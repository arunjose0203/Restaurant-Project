using System.Net.Sockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace Restaurant.Api;
public static class ReceiptRenderer {
    public static string Render(string business, string heading, IEnumerable<string> lines, string footer, int width) {
        if (width is not (32 or 48)) throw new ArgumentException("Choose 32 columns (58mm) or 48 columns (80mm).");
        // Strip control characters, including ESC/POS commands, from restaurant and guest text.
        static string Clean(string s) => new(s.Where(c => !char.IsControl(c)).ToArray());
        var result = new StringBuilder();
        foreach (var line in new[] { business, heading, new string('-', width) }.Concat(lines).Concat([new string('-', width), footer])) {
            var text = Clean(line); if (text.Length == 0) result.AppendLine();
            while (text.Length > 0) { var take = Math.Min(width, text.Length); result.AppendLine(text[..take]); text = text[take..]; }
        }
        return result.ToString();
    }
    public static byte[] EscPos(string text) => new byte[] { 27, 64 }.Concat(Encoding.ASCII.GetBytes(text.Replace("₹", "INR "))).Concat(new byte[] { 10, 10, 10, 29, 86, 0 }).ToArray();
}
public static class IntegrationEndpoints {
    public static void MapIntegrations(this WebApplication app) {
        var api = app.MapGroup("/api/pos").RequireAuthorization();
        api.MapPut("/devices", async (DeviceRequest r, RestaurantDb db, ClaimsPrincipal user) => {
            if (r.Token.Length > 256 || !(r.Token.StartsWith("ExponentPushToken[") || r.Token.StartsWith("ExpoPushToken[")) || !r.Token.EndsWith(']')) throw new ArgumentException("Invalid Expo push token.");
            var device = await db.Devices.IgnoreQueryFilters().SingleOrDefaultAsync(d => d.Token == r.Token);
            // Re-registering a device transfers delivery to the currently authenticated staff member.
            if (device != null) { db.UseBranch(device.BranchId); db.Devices.Remove(device); await db.SaveChangesAsync(); db.UseBranch(int.Parse(user.FindFirst("branch")!.Value)); }
            db.Devices.Add(new() { UserId = PosService.Actor(user), Token = r.Token }); await db.SaveChangesAsync(); return Results.Ok(new {ok=true});
        });
        api.MapPost("/devices/remove", async (DeviceRequest r, RestaurantDb db, ClaimsPrincipal user) => { await db.Devices.Where(d => d.Token == r.Token && d.UserId == PosService.Actor(user)).ExecuteDeleteAsync(); return Results.Ok(new {ok=true}); });
        api.MapGet("/print/{kind}/{id:guid}", async (string kind, Guid id, int? width, string? format, RestaurantDb db, ClaimsPrincipal user) => {
            var text = await Receipt(db, user, kind, id, width ?? 48);
            return format == "escpos" ? Results.File(ReceiptRenderer.EscPos(text), "application/octet-stream", $"{kind}-{id}.bin") : Results.Text(text);
        });
        api.MapPost("/print/{kind}/{id:guid}", async (string kind, Guid id, PrintRequest r, RestaurantDb db, ClaimsPrincipal user, IConfiguration config, IHttpClientFactory factory) => {
            var text = await Receipt(db, user, kind, id, r.Width);
            if (r.Printer is not ("kitchen" or "cashier")) throw new ArgumentException("Choose kitchen or cashier printer.");
            var prefix = $"Printers:{db.CurrentBranchId}:{r.Printer}";
            var host = config[prefix + ":Host"];
            var bridge = config[prefix + ":BridgeUrl"];
            if (string.IsNullOrEmpty(host) && string.IsNullOrEmpty(bridge)) return Results.Problem("Printer is not configured. Use browser printing or configure a network printer / local USB-Bluetooth bridge.", statusCode: 503);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (!string.IsNullOrEmpty(host)) {
                using var client = new TcpClient(); await client.ConnectAsync(host, int.TryParse(config[prefix + ":Port"], out var port) ? port : 9100, timeout.Token);
                await client.GetStream().WriteAsync(ReceiptRenderer.EscPos(text), timeout.Token);
            } else {
                var client = factory.CreateClient(); if (!string.IsNullOrEmpty(config[prefix + ":BridgeToken"])) client.DefaultRequestHeaders.Authorization = new("Bearer", config[prefix + ":BridgeToken"]);
                using var response = await client.PostAsync(bridge, new ByteArrayContent(ReceiptRenderer.EscPos(text)), timeout.Token); response.EnsureSuccessStatusCode();
            }
            PosService.Audit(db, PosService.Actor(user), "Print submitted", id.ToString(), r.Printer, new { kind, r.Width }); await db.SaveChangesAsync();
            return Results.Ok(new { message = "Sent to printer. Verify physical output before reprinting." });
        });
        api.MapPost("/gateway/{id:guid}/order", async (Guid id, RestaurantDb db, IConfiguration config, IHttpClientFactory factory) => {
            if (string.IsNullOrEmpty(config["Payments:Razorpay:KeyId"]) || string.IsNullOrEmpty(config["Payments:Razorpay:Secret"])) return Results.Problem("Payment provider is not configured.", statusCode: 503);
            await using var tx = await db.Database.BeginTransactionAsync(); var session = await PosService.LockSession(db, id); if (session is null) return Results.NotFound();
            var quote = await PosService.Quote(db, session);
            if (session.ClosedAt != null || quote.Outstanding <= 0 || await db.Orders.AnyAsync(o => o.SessionId == id && o.Status != "Served" && o.Status != "Voided")) throw new ArgumentException("Serve all orders before payment.");
            var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(config["Payments:Razorpay:KeyId"] + ":" + config["Payments:Razorpay:Secret"])));
            using var response = await client.PostAsJsonAsync("https://api.razorpay.com/v1/orders", new { amount = (long)(quote.Outstanding * 100), currency = "INR", receipt = Guid.NewGuid().ToString("N") }); response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            var order = new GatewayOrder { SessionId = id, Amount = quote.Outstanding, ProviderOrderId = payload.GetProperty("id").GetString()! }; db.GatewayOrders.Add(order); session.QuoteJson = JsonSerializer.Serialize(quote);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new { order.Id, order.ProviderOrderId, order.Amount, keyId = config["Payments:Razorpay:KeyId"] });
        }).RequireAuthorization(p => p.RequireRole("Cashier", "Admin"));
        api.MapPost("/gateway/{id:guid}/confirm", async (Guid id, GatewayConfirmation r, RestaurantDb db, ClaimsPrincipal user, IConfiguration config, IHttpClientFactory factory) => {
            var secret = config["Payments:Razorpay:Secret"]; if (string.IsNullOrEmpty(secret)) return Results.Problem("Payment provider is not configured.", statusCode: 503);
            var gateway = await db.GatewayOrders.SingleOrDefaultAsync(g => g.Id == id); if (gateway is null) return Results.NotFound();
            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(gateway.ProviderOrderId + "|" + r.PaymentId));
            byte[] actual; try { actual = Convert.FromHexString(r.Signature); } catch { return Results.BadRequest(new { message = "Invalid signature." }); }
            if (!CryptographicOperations.FixedTimeEquals(expected, actual)) return Results.BadRequest(new { message = "Invalid signature." });
            var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(config["Payments:Razorpay:KeyId"] + ":" + secret)));
            var payment = await client.GetFromJsonAsync<JsonElement>("https://api.razorpay.com/v1/payments/" + Uri.EscapeDataString(r.PaymentId));
            if (payment.GetProperty("status").GetString() != "captured" || payment.GetProperty("order_id").GetString() != gateway.ProviderOrderId || payment.GetProperty("currency").GetString() != "INR" || payment.GetProperty("amount").GetInt64() != (long)(gateway.Amount * 100)) throw new ArgumentException("Provider has not confirmed the expected captured payment.");
            await using var tx = await db.Database.BeginTransactionAsync(); var session = await PosService.LockSession(db, gateway.SessionId); if (gateway.Paid) return Results.Ok(new {ok=true});
            await db.Entry(gateway).ReloadAsync(); if (gateway.Paid) return Results.Ok(new {ok=true});
            var quote = await PosService.Quote(db, session!);
            await PosService.Pay(db, session!, new(id, r.PaymentMethodId, gateway.Amount, quote.Total, r.PaymentId), PosService.Actor(user), r.PaymentId);
            gateway.Paid = true; await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new {ok=true});
        }).RequireAuthorization(p => p.RequireRole("Cashier", "Admin"));
    }
    static async Task<string> Receipt(RestaurantDb db, ClaimsPrincipal user, string kind, Guid id, int width) {
        var settings = await PosService.Settings(db); List<string> lines = []; string heading;
        if (kind == "kot") {
            var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id) ?? throw new ArgumentException("Order not found.");
            if (user.IsInRole("Waiter") && order.WaiterId != PosService.Actor(user)) throw new ArgumentException("Order belongs to another waiter.");
            heading = $"KOT #{order.Id.ToString()[..8]} / Table {order.TableId}";
            var waiter = await db.Users.SingleAsync(u => u.Id == order.WaiterId);
            lines.Add(waiter.Name); lines.Add(order.CreatedAt.ToString("u")); lines.AddRange(order.Items.Select(i => $"{i.Quantity} x {i.Name} [{i.Station}]")); lines.Add(order.Instructions);
        } else if (kind == "bill") {
            if (!user.IsInRole("Cashier") && !user.IsInRole("Admin")) throw new ArgumentException("Cashier access required.");
            var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == id) ?? throw new ArgumentException("Visit not found."); var quote = await PosService.Quote(db, session);
            heading = $"BILL / Table {session.TableId} / {id.ToString()[..8]}";
            lines.Add(settings.Address); lines.Add("Tax ID: " + settings.TaxId);
            var orders = await db.Orders.Include(o => o.Items).Where(o => o.SessionId == id && o.Status != "Voided").ToListAsync();
            lines.AddRange(orders.SelectMany(o => o.Items).Select(i => $"{i.Quantity} x {i.Name}  INR {i.Quantity * i.UnitPrice:0.00}"));
            lines.Add($"Subtotal {quote.Subtotal:0.00}"); lines.Add($"Discount -{quote.Discount:0.00}"); lines.Add($"Service {quote.ServiceCharge:0.00}"); lines.AddRange(quote.Taxes.Select(t => $"{t.Name} {t.Amount:0.00}")); lines.Add($"Tip {quote.Tip:0.00}"); lines.Add($"TOTAL INR {quote.Total:0.00}"); lines.Add($"PAID {quote.Paid:0.00}"); lines.Add($"BALANCE {quote.Outstanding:0.00}");
        } else throw new ArgumentException("Choose kot or bill.");
        return ReceiptRenderer.Render(settings.BusinessName, heading, lines, settings.ReceiptFooter, width);
    }
}
public record PrintRequest(string Printer, int Width = 48);
public record GatewayConfirmation(string PaymentId, string Signature, int PaymentMethodId);
