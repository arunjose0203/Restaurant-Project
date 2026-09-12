using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Restaurant.Api;

public static class PosService {
    public static Guid Actor(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static void Audit(RestaurantDb db, Guid actor, string action, string subject, string reason, object? detail = null) => db.Audit.Add(new() { ActorId = actor, Action = action, Subject = subject, Reason = reason, Detail = JsonSerializer.Serialize(detail) });
    public static async Task<TableSession?> LockSession(RestaurantDb db, Guid id) {
        // Every operation on a visit locks the same session row, including partial payments.
        return await db.Sessions.FromSqlInterpolated($"SELECT * FROM \"Sessions\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync();
    }
    public static async Task<PosSettings> Settings(RestaurantDb db) => await db.Settings.SingleOrDefaultAsync() ?? new PosSettings();
    public static async Task<BillQuote> Quote(RestaurantDb db, TableSession session) {
        var paid = await db.Payments.Where(p => p.SessionId == session.Id).SumAsync(p => p.Amount);
        if (session.QuoteJson.Length > 0) {
            var frozen = JsonSerializer.Deserialize<BillQuote>(session.QuoteJson)!;
            return frozen with { Paid = paid, Outstanding = BillingEngine.Round(frozen.Total - paid) };
        }
        return BillingEngine.Calculate(await db.Orders.Include(o => o.Items).Where(o => o.SessionId == session.Id).ToListAsync(), await Settings(db), session.DiscountKind, session.DiscountValue, session.Tip, paid);
    }
    public static async Task<Order> CreateOrder(RestaurantDb db, OrderRequest request, Guid waiter) {
        if (request.ClientRequestId is null || request.ClientRequestId == Guid.Empty) throw new ArgumentException("A durable request ID is required.");
        if (request.Items is not { Count: > 0 and <= 100 } || request.Items.Any(i => i.Quantity is < 1 or > 99) || (request.Instructions?.Length ?? 0) > 500) throw new ArgumentException("Choose 1–100 lines, quantities 1–99, and up to 500 instruction characters.");
        var previous = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == request.ClientRequestId);
        if (previous != null) {
            if (!SameRequest(previous, request, waiter)) throw new ArgumentException("Request ID already belongs to another order.");
            return previous;
        }
        var table = await db.Tables.FromSqlInterpolated($"SELECT * FROM \"Tables\" WHERE \"Id\"={request.TableId} FOR UPDATE").SingleOrDefaultAsync();
        if (table is null || !table.Active) throw new ArgumentException("Table unavailable.");
        // Recheck after the table lock: another retry may have committed while this call waited.
        previous = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == request.ClientRequestId);
        if (previous != null) { if (!SameRequest(previous, request, waiter)) throw new ArgumentException("Request ID already belongs to another order."); return previous; }
        var session = await db.Sessions.SingleOrDefaultAsync(s => s.TableId == table.Id && s.ClosedAt == null);
        if (session != null) {
            session = await LockSession(db, session.Id);
            if (session!.QuoteJson.Length > 0) throw new ArgumentException("Payment has started; finish this visit before adding food.");
        } else { session = new() { TableId = table.Id }; db.Sessions.Add(session); }
        var order = new Order { Id = request.ClientRequestId.Value, TableId = table.Id, SessionId = session.Id, WaiterId = waiter, Instructions = request.Instructions?.Trim() ?? "" };
        // Stable lock order prevents two orders with opposite item ordering from deadlocking.
        var menu = new Dictionary<int, MenuItem>();
        foreach (var id in request.Items.Select(i => i.MenuItemId).Distinct().Order()) {
            var item = await db.MenuItems.FromSqlInterpolated($"SELECT * FROM \"MenuItems\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync();
            if (item is null || !item.Active || !item.Available) throw new ArgumentException("A selected item is unavailable.");
            var quantity = request.Items.Where(i => i.MenuItemId == id).Sum(i => i.Quantity);
            if (quantity > 99 || (item.Stock.HasValue && item.Stock < quantity)) throw new ArgumentException("Insufficient stock or quantity exceeds 99.");
            if (item.Stock.HasValue) { item.Stock -= quantity; if (item.Stock == 0) item.Available = false; }
            menu.Add(id, item);
        }
        foreach (var line in request.Items) {
            var item = menu[line.MenuItemId];
            var portions = JsonSerializer.Deserialize<List<Portion>>(item.PortionsJson) ?? [];
            var groups = JsonSerializer.Deserialize<List<ModifierGroup>>(item.ModifiersJson) ?? [];
            var portion = portions.SingleOrDefault(p => p.Name == line.Portion);
            if ((portions.Count > 0 && portion is null) || (portions.Count == 0 && !string.IsNullOrEmpty(line.Portion))) throw new ArgumentException("Choose a valid portion.");
            var selected = line.Modifiers ?? [];
            if (selected.Select(m => m.Group).Distinct().Count() != selected.Count || groups.Any(g => g.Required && !selected.Any(s => s.Group == g.Name))) throw new ArgumentException("Choose one option per required modifier group.");
            var price = portion?.Price ?? item.Price;
            foreach (var modifier in selected) {
                var option = groups.SingleOrDefault(g => g.Name == modifier.Group)?.Options.SingleOrDefault(o => o.Name == modifier.Option);
                if (option is null) throw new ArgumentException("Invalid modifier.");
                price += option.Price;
            }
            order.Items.Add(new() { MenuItemId = item.Id, Name = item.Name + (portion is null ? "" : $" · {portion.Name}") + (selected.Count == 0 ? "" : " · " + string.Join(", ", selected.Select(s => s.Option))), Quantity = line.Quantity, UnitPrice = BillingEngine.Round(price), Station = item.Station, Portion = line.Portion ?? "", ModifiersJson = JsonSerializer.Serialize(selected.OrderBy(m => m.Group)) });
        }
        db.Orders.Add(order); db.OrderEvents.Add(new() { OrderId = order.Id, UserId = waiter, Status = "New" });
        await db.SaveChangesAsync(); return order;
    }
    public static bool SameRequest(Order order, OrderRequest request, Guid waiter) {
        static string Key(int id, string? portion, IEnumerable<SelectedModifier> mods) => $"{id}|{portion ?? ""}|{JsonSerializer.Serialize(mods.OrderBy(m => m.Group))}";
        var actual = order.Items.GroupBy(i => Key(i.MenuItemId, i.Portion, JsonSerializer.Deserialize<List<SelectedModifier>>(i.ModifiersJson) ?? [])).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        var wanted = request.Items.GroupBy(i => Key(i.MenuItemId, i.Portion, i.Modifiers ?? [])).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        return order.TableId == request.TableId && order.WaiterId == waiter && order.Instructions == (request.Instructions?.Trim() ?? "") && actual.Count == wanted.Count && actual.All(p => wanted.GetValueOrDefault(p.Key) == p.Value);
    }
    public static async Task<PaymentEntry> Pay(RestaurantDb db, TableSession session, PaymentRequest r, Guid cashier, string providerId = "") {
        if (r.RequestId == Guid.Empty || r.Amount <= 0 || BillingEngine.Round(r.Amount) != r.Amount || (r.Reference?.Length ?? 0) > 120) throw new ArgumentException("Provide a request ID, positive amount with two decimals, and a reference up to 120 characters.");
        var previous = await db.Payments.SingleOrDefaultAsync(p => p.Id == r.RequestId);
        if (previous != null) {
            if (previous.SessionId != session.Id || previous.Amount != r.Amount || previous.PaymentMethodId != r.PaymentMethodId || previous.Reference != (r.Reference?.Trim() ?? "")) throw new ArgumentException("Payment request ID belongs to different details.");
            return previous;
        }
        var orders = await db.Orders.Include(o => o.Items).Where(o => o.SessionId == session.Id && o.Status != "Voided").ToListAsync();
        if (session.ClosedAt != null || orders.Count == 0 || orders.Any(o => o.Status != "Served")) throw new ArgumentException("Serve all orders before collecting payment.");
        if (!await db.PaymentMethods.AnyAsync(m => m.Id == r.PaymentMethodId && m.Active)) throw new ArgumentException("Choose an active payment method.");
        var quote = await Quote(db, session);
        if (quote.Total != r.ExpectedTotal || r.Amount > quote.Outstanding) throw new ArgumentException("Bill changed or payment exceeds the balance. Review the bill.");
        session.QuoteJson = JsonSerializer.Serialize(quote);
        var payment = new PaymentEntry { Id = r.RequestId, SessionId = session.Id, CashierId = cashier, Amount = r.Amount, PaymentMethodId = r.PaymentMethodId, Reference = r.Reference?.Trim() ?? "", ProviderPaymentId = providerId };
        db.Payments.Add(payment);
        Audit(db, cashier, "Payment", session.Id.ToString(), "Confirmed tender", new { payment.Id, payment.Amount, payment.PaymentMethodId });
        if (r.Amount == quote.Outstanding) {
            session.ClosedAt = DateTime.UtcNow;
            db.Bills.Add(new() { SessionId = session.Id, TableId = session.TableId, CashierId = cashier, Total = quote.Total, PaymentMethodId = r.PaymentMethodId, Reference = r.Reference ?? "", QuoteJson = session.QuoteJson });
            foreach (var order in orders) { order.Status = "Paid"; order.UpdatedAt = DateTime.UtcNow; db.OrderEvents.Add(new() { OrderId = order.Id, UserId = cashier, Status = "Paid" }); }
        }
        await db.SaveChangesAsync(); return payment;
    }
}
