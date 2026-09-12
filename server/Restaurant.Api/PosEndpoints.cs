using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Restaurant.Api;

public static class PosEndpoints {
    public static void MapPos(this WebApplication app) {
        var staff = app.MapGroup("/api/pos").RequireAuthorization();
        var cash = staff.MapGroup("").RequireAuthorization(p => p.RequireRole("Cashier", "Admin"));
        var admin = staff.MapGroup("/admin").RequireAuthorization(p => p.RequireRole("Admin"));
        staff.MapGet("/settings", async (RestaurantDb db) => await PosService.Settings(db));
        admin.MapPut("/settings", async (PosSettings r, RestaurantDb db, ClaimsPrincipal actor) => {
            var taxes = JsonSerializer.Deserialize<List<TaxRule>>(r.TaxRulesJson) ?? [];
            if (taxes.Count > 10 || taxes.Any(t => string.IsNullOrWhiteSpace(t.Name) || t.Name.Length > 60 || t.Percent is < 0 or > 100) || r.ServicePercent is < 0 or > 100 || r.BusinessName.Length > 100 || r.Address.Length > 500 || r.TaxId.Length > 100 || r.UpiId.Length > 100 || r.ReceiptFooter.Length > 300) throw new ArgumentException("Invalid receipt or tax configuration.");
            var settings = await db.Settings.SingleOrDefaultAsync();
            if (settings is null) { settings = new(); db.Settings.Add(settings); }
            settings.BusinessName = r.BusinessName; settings.Address = r.Address; settings.TaxId = r.TaxId; settings.UpiId = r.UpiId; settings.TaxRulesJson = JsonSerializer.Serialize(taxes); settings.ServicePercent = r.ServicePercent; settings.ReceiptFooter = r.ReceiptFooter;
            PosService.Audit(db, PosService.Actor(actor), "Settings", "Billing", "Configuration changed", settings);
            await db.SaveChangesAsync(); return Results.Ok(settings);
        });
        cash.MapGet("/sessions/{id:guid}", async (Guid id, RestaurantDb db) => {
            var session = await db.Sessions.SingleOrDefaultAsync(s => s.Id == id); if (session is null) return Results.NotFound();
            return Results.Ok(new { session, quote = await PosService.Quote(db, session), payments = await db.Payments.Where(p => p.SessionId == id).OrderBy(p => p.CreatedAt).ToListAsync() });
        });
        cash.MapPost("/sessions/{id:guid}/payments", async (Guid id, PaymentRequest r, RestaurantDb db, ClaimsPrincipal user) => {
            await using var tx = await db.Database.BeginTransactionAsync();
            var session = await PosService.LockSession(db, id); if (session is null) return Results.NotFound();
            var payment = await PosService.Pay(db, session, r, PosService.Actor(user)); await tx.CommitAsync(); return Results.Ok(payment);
        });
        admin.MapPost("/sessions/{id:guid}/discount", async (Guid id, DiscountRequest r, RestaurantDb db, ClaimsPrincipal user) => {
            if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 300) throw new ArgumentException("A discount reason is required (up to 300 characters).");
            await using var tx = await db.Database.BeginTransactionAsync(); var session = await PosService.LockSession(db, id);
            if (session is null) return Results.NotFound(); EnsureEditable(session);
            session.DiscountKind = r.Kind; session.DiscountValue = r.Value;
            await PosService.Quote(db, session); session.SplitJson = "[]";
            PosService.Audit(db, PosService.Actor(user), "Discount", id.ToString(), r.Reason, r);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new {ok=true});
        });
        cash.MapPost("/sessions/{id:guid}/tip", async (Guid id, TipRequest r, RestaurantDb db, ClaimsPrincipal user) => {
            await using var tx = await db.Database.BeginTransactionAsync(); var session = await PosService.LockSession(db, id); if (session is null) return Results.NotFound(); EnsureEditable(session);
            if (r.Amount < 0 || r.Amount > 100000 || BillingEngine.Round(r.Amount) != r.Amount) throw new ArgumentException("Enter a tip with two decimals between 0 and 100000.");
            session.Tip = r.Amount; session.SplitJson = "[]"; PosService.Audit(db, PosService.Actor(user), "Tip", id.ToString(), "Guest-selected tip", r);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new {ok=true});
        });
        cash.MapPost("/sessions/{id:guid}/split", async (Guid id, SplitRequest r, RestaurantDb db, ClaimsPrincipal user) => {
            await using var tx = await db.Database.BeginTransactionAsync(); var session = await PosService.LockSession(db, id); if (session is null) return Results.NotFound(); EnsureEditable(session);
            var quote = await PosService.Quote(db, session); List<SplitPart> parts;
            if (r.Mode == "Equal") parts = BillingEngine.SplitEqual(quote.Total, r.Guests).Select((amount, i) => new SplitPart($"Guest {i + 1}", amount, null)).ToList();
            else {
                parts = r.Parts ?? [];
                if (parts.Count is < 1 or > 100 || parts.Any(p => p.Amount < 0 || BillingEngine.Round(p.Amount) != p.Amount || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 80)) throw new ArgumentException("Supply valid split parts.");
                if (r.Mode == "Items") {
                    var items = await db.OrderItems.Where(i => db.Orders.Any(o => o.Id == i.OrderId && o.SessionId == id && o.Status != "Voided")).ToListAsync();
                    var allocated = parts.SelectMany(p => p.ItemIds ?? []).ToList();
                    if (allocated.Count != items.Count || allocated.Distinct().Count() != items.Count || items.Any(i => !allocated.Contains(i.Id))) throw new ArgumentException("Assign every item line exactly once.");
                    decimal assigned = 0;
                    parts = parts.Select((p, index) => { var weight = items.Where(i => (p.ItemIds ?? []).Contains(i.Id)).Sum(i => i.Quantity * i.UnitPrice); var amount = index == parts.Count - 1 ? quote.Total - assigned : quote.Subtotal == 0 ? 0 : BillingEngine.Round(quote.Total * weight / quote.Subtotal); assigned += amount; return p with { Amount = amount }; }).ToList();
                } else if (r.Mode != "Custom") throw new ArgumentException("Choose Equal, Items, or Custom.");
                if (parts.Sum(p => p.Amount) != quote.Total) throw new ArgumentException("Split amounts must add up to the full bill.");
            }
            session.SplitJson = JsonSerializer.Serialize(parts); PosService.Audit(db, PosService.Actor(user), "Split", id.ToString(), r.Mode, parts);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(parts);
        });
        staff.MapPost("/menu/{id:int}/availability", async (int id, AvailabilityRequest r, RestaurantDb db, ClaimsPrincipal user) => {
            if (r.Stock < 0) throw new ArgumentException("Stock cannot be negative.");
            await using var tx = await db.Database.BeginTransactionAsync(); var item = await db.MenuItems.FromSqlInterpolated($"SELECT * FROM \"MenuItems\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(); if (item is null) return Results.NotFound();
            item.Available = r.Available && r.Stock != 0; item.Stock = r.Stock;
            PosService.Audit(db, PosService.Actor(user), "Stock", id.ToString(), "Kitchen availability", r); await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(item);
        }).RequireAuthorization(p => p.RequireRole("Kitchen", "Admin"));
        admin.MapPost("/orders/{id:guid}/void", async (Guid id, VoidRequest r, RestaurantDb db, ClaimsPrincipal user) => {
            if (!new[] { "Kitchen Error", "Customer Changed Mind", "Quality Issue", "Spillage" }.Contains(r.Reason)) throw new ArgumentException("Choose an audit reason.");
            await using var tx = await db.Database.BeginTransactionAsync(); var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id); if (order is null) return Results.NotFound();
            var session = await PosService.LockSession(db, order.SessionId); EnsureEditable(session!);
            if (order.Status == "Voided") return Results.Ok(new {ok=true}); if (order.Status == "Paid") throw new ArgumentException("Paid orders cannot be voided.");
            order.Status = "Voided"; order.UpdatedAt = DateTime.UtcNow; session!.SplitJson = "[]";
            db.OrderEvents.Add(new() { OrderId = id, UserId = PosService.Actor(user), Status = "Voided" });
            PosService.Audit(db, PosService.Actor(user), "Void", id.ToString(), r.Reason, new { loss = order.Items.Sum(i => i.Quantity * i.UnitPrice) });
            // A void records waste; stock is not silently returned to inventory.
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new {ok=true});
        });
        staff.MapPost("/sessions/{id:guid}/transfer", async (Guid id, TransferRequest r, RestaurantDb db, ClaimsPrincipal user) => {
            await using var tx = await db.Database.BeginTransactionAsync();
            var source = await db.Sessions.SingleOrDefaultAsync(s => s.Id == id); if (source is null) return Results.NotFound();
            if (source.TableId == r.TableId) throw new ArgumentException("Choose another table.");
            foreach (var tableId in new[] { source.TableId, r.TableId }.Order()) await db.Tables.FromSqlInterpolated($"SELECT * FROM \"Tables\" WHERE \"Id\"={tableId} FOR UPDATE").SingleOrDefaultAsync();
            source = await PosService.LockSession(db, id); EnsureEditable(source!);
            var targetTable = await db.Tables.SingleOrDefaultAsync(t => t.Id == r.TableId && t.Active); if (targetTable is null) throw new ArgumentException("Target table unavailable.");
            var target = await db.Sessions.SingleOrDefaultAsync(s => s.TableId == r.TableId && s.ClosedAt == null);
            if (target != null) { if (!r.Merge) throw new ArgumentException("Target table occupied; select merge."); target = await PosService.LockSession(db, target.Id); EnsureEditable(target!); if (source!.DiscountValue != 0 || target!.DiscountValue != 0 || source.Tip != 0 || target.Tip != 0) throw new ArgumentException("Remove discounts and tips before merging visits."); }
            var orders = await db.Orders.Where(o => o.SessionId == id).ToListAsync();
            foreach (var order in orders) { order.TableId = r.TableId; if (target != null) order.SessionId = target.Id; }
            if (target is null) source!.TableId = r.TableId; else { source!.ClosedAt = DateTime.UtcNow; target.SplitJson = "[]"; }
            source.SplitJson = "[]"; PosService.Audit(db, PosService.Actor(user), r.Merge ? "Merge" : "Transfer", id.ToString(), "Floor transfer", r);
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(new {ok=true});
        }).RequireAuthorization(p => p.RequireRole("Cashier", "Admin"));
        staff.MapGet("/guest-requests", async (RestaurantDb db) => await db.GuestRequests.Where(r => !r.Resolved).OrderBy(r => r.CreatedAt).ToListAsync());
        staff.MapPost("/guest-requests/{id:guid}/resolve", async (Guid id, RestaurantDb db) => { var r = await db.GuestRequests.SingleOrDefaultAsync(r => r.Id == id); if (r is null) return Results.NotFound(); r.Resolved = true; await db.SaveChangesAsync(); return Results.Ok(new {ok=true}); });
        admin.MapGet("/audit", async (int? page, RestaurantDb db) => await db.Audit.OrderByDescending(a => a.Id).Skip(Math.Clamp((page ?? 1) - 1, 0, 100000) * 50).Take(50).ToListAsync());
        admin.MapGet("/report", Report);
        admin.MapGet("/report.csv", async (DateTime from, DateTime to, RestaurantDb db) => {
            ValidateRange(from, to);
            var payments = await db.Payments.Where(p => p.CreatedAt >= from && p.CreatedAt < to).OrderBy(p => p.CreatedAt).ToListAsync();
            static string Cell(string value) => "\"" + (value.StartsWith('=') || value.StartsWith('+') || value.StartsWith('-') || value.StartsWith('@') ? "'" : "") + value.Replace("\"", "\"\"") + "\"";
            var csv = "Payment,Visit,UTC,Method,Amount,Reference\r\n" + string.Join("\r\n", payments.Select(p => string.Join(",", p.Id, p.SessionId, p.CreatedAt.ToString("O"), p.PaymentMethodId, p.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), Cell(p.Reference))));
            return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", "tableflow-payments.csv");
        });
    }
    static void EnsureEditable(TableSession session) { if (session.ClosedAt != null || session.QuoteJson.Length > 0) throw new ArgumentException("Visit is closed or payment has started."); }
    static void ValidateRange(DateTime from, DateTime to) { if (from.Kind != DateTimeKind.Utc || to.Kind != DateTimeKind.Utc || to <= from || to - from > TimeSpan.FromDays(366)) throw new ArgumentException("Use UTC dates spanning up to one year."); }
    static async Task<IResult> Report(DateTime from, DateTime to, RestaurantDb db) {
        ValidateRange(from, to);
        var bills = await db.Bills.Where(b => b.PaidAt >= from && b.PaidAt < to).ToListAsync();
        var payments = await db.Payments.Where(p => p.CreatedAt >= from && p.CreatedAt < to).ToListAsync();
        var orders = await db.Orders.Include(o => o.Items).Where(o => o.CreatedAt >= from && o.CreatedAt < to).ToListAsync();
        var quotes = bills.Where(b => b.QuoteJson.Length > 0).Select(b => JsonSerializer.Deserialize<BillQuote>(b.QuoteJson)!).ToList();
        var events = await db.OrderEvents.Where(e => e.CreatedAt >= from && e.CreatedAt < to && (e.Status == "Preparing" || e.Status == "Ready")).ToListAsync();
        var prep = events.GroupBy(e => e.OrderId).Select(g => new { order = orders.SingleOrDefault(o => o.Id == g.Key), start = g.FirstOrDefault(e => e.Status == "Preparing")?.CreatedAt, ready = g.FirstOrDefault(e => e.Status == "Ready")?.CreatedAt }).Where(x => x.order != null && x.start != null && x.ready >= x.start).ToList();
        return Results.Ok(new {
            from, to, gross = quotes.Sum(q => q.Subtotal), discounts = quotes.Sum(q => q.Discount), taxes = quotes.Sum(q => q.Taxes.Sum(t => t.Amount)), serviceCharge = quotes.Sum(q => q.ServiceCharge), tips = quotes.Sum(q => q.Tip), closedSales = bills.Sum(b => b.Total), tenderCollected = payments.Sum(p => p.Amount), closedVisits = bills.Count,
            byMethod = payments.GroupBy(p => p.PaymentMethodId).Select(g => new { methodId = g.Key, total = g.Sum(p => p.Amount), count = g.Count() }),
            dishes = orders.Where(o => o.Status == "Paid").SelectMany(o => o.Items).GroupBy(i => i.Name).Select(g => new { name = g.Key, quantity = g.Sum(i => i.Quantity), revenue = g.Sum(i => i.Quantity * i.UnitPrice) }).OrderByDescending(i => i.revenue),
            hours = orders.GroupBy(o => o.CreatedAt.Hour).Select(g => new { utcHour = g.Key, count = g.Count() }),
            servers = orders.Where(o => o.Status == "Paid").GroupBy(o => o.WaiterId).Select(g => new { userId = g.Key, sales = g.Sum(o => o.Items.Sum(i => i.Quantity * i.UnitPrice)) }),
            preparation = prep.SelectMany(p => p.order!.Items.Select(i => i.Station).Distinct().Select(station => new { station, minutes = (p.ready!.Value - p.start!.Value).TotalMinutes })).GroupBy(p => p.station).Select(g => new { station = g.Key, averageMinutes = g.Average(p => p.minutes) }),
            voids = orders.Count(o => o.Status == "Voided"), feedback = await db.Feedback.Where(f => f.CreatedAt >= from && f.CreatedAt < to).ToListAsync()
        });
    }
}
public record TipRequest(decimal Amount);
