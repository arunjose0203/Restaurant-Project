using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Restaurant.Api;

public abstract class BranchRecord { public int BranchId { get; set; } = 1; }
public class Branch { public int Id { get; set; } public string Name { get; set; } = "Main restaurant"; public string TimeZone { get; set; } = "Asia/Kolkata"; public bool Active { get; set; } = true; }
public class StaffBranch { public Guid UserId { get; set; } public int BranchId { get; set; } public string Role { get; set; } = "Waiter"; }
public class PosSettings : BranchRecord {
    public int Id { get; set; }
    public string BusinessName { get; set; } = "Tableflow";
    public string Address { get; set; } = "";
    public string TaxId { get; set; } = "";
    public string UpiId { get; set; } = "";
    public string TaxRulesJson { get; set; } = "[]";
    public decimal ServicePercent { get; set; }
    public string ReceiptFooter { get; set; } = "Thank you for dining with us.";
}
public record TaxRule(string Name, decimal Percent);
public class PaymentEntry : BranchRecord {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid CashierId { get; set; }
    public int PaymentMethodId { get; set; }
    public decimal Amount { get; set; }
    public string Reference { get; set; } = "";
    public string ProviderPaymentId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class AuditEntry : BranchRecord {
    public long Id { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Detail { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class DeviceToken : BranchRecord {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public class OutboxMessage : BranchRecord {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "StateChanged";
    public Guid? UserId { get; set; }
    public string Payload { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAt { get; set; }
    public int Attempts { get; set; }
    public string LastError { get; set; } = "";
}
public class GuestRequest : BranchRecord {
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TableId { get; set; }
    public string Kind { get; set; } = "Waiter";
    public bool Resolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class GuestFeedback : BranchRecord {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class GatewayOrder : BranchRecord {
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string ProviderOrderId { get; set; } = "";
    public decimal Amount { get; set; }
    public bool Paid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public record Portion(string Name, decimal Price);
public record ModifierOption(string Name, decimal Price);
public record ModifierGroup(string Name, bool Required, List<ModifierOption> Options);
public record SelectedModifier(string Group, string Option);
public record BillTax(string Name, decimal Amount);
public record BillQuote(decimal Subtotal, decimal Discount, decimal ServiceCharge, List<BillTax> Taxes, decimal Tip, decimal Total, decimal Paid, decimal Outstanding);
public record PaymentRequest(Guid RequestId, int PaymentMethodId, decimal Amount, decimal ExpectedTotal, string? Reference);
public record DiscountRequest(string Kind, decimal Value, string Reason);
public record SplitPart(string Name, decimal Amount, List<Guid>? ItemIds);
public record SplitRequest(string Mode, int Guests, List<SplitPart>? Parts);
public record TransferRequest(int TableId, bool Merge);
public record AvailabilityRequest(bool Available, int? Stock);
public record VoidRequest(string Reason);
public record PinRequest(string Pin);
public record PinLoginRequest(Guid UserId, string Pin, int BranchId);
public record DeviceRequest(string Token);

public static class BillingEngine {
    public static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    public static BillQuote Calculate(IEnumerable<Order> orders, PosSettings settings, string discountKind, decimal discountValue, decimal tip, decimal paid) {
        var subtotal = Round(orders.Where(o => o.Status != "Voided").Sum(o => o.Items.Sum(i => i.Quantity * i.UnitPrice)));
        if (discountValue < 0 || tip < 0 || tip > 100000 || paid < 0) throw new ArgumentException("Amounts must be nonnegative and tip must not exceed 100000.");
        if (discountKind is not ("None" or "Percent" or "Flat")) throw new ArgumentException("Choose None, Percent, or Flat discount.");
        if (discountKind == "Percent" && discountValue > 100) throw new ArgumentException("Discount percentage cannot exceed 100.");
        var discount = Round(discountKind == "Percent" ? subtotal * discountValue / 100 : discountKind == "Flat" ? discountValue : 0);
        if (discount > subtotal) throw new ArgumentException("Discount exceeds subtotal.");
        var net = subtotal - discount;
        var service = Round(net * settings.ServicePercent / 100);
        // Each configured tax applies to the discounted subtotal plus service charge.
        var rules = JsonSerializer.Deserialize<List<TaxRule>>(settings.TaxRulesJson) ?? [];
        if (settings.ServicePercent is < 0 or > 100 || rules.Any(r => r.Percent is < 0 or > 100)) throw new ArgumentException("Invalid tax or service percentage.");
        var taxes = rules.Select(r => new BillTax(r.Name, Round((net + service) * r.Percent / 100))).ToList();
        var total = Round(net + service + taxes.Sum(t => t.Amount) + tip);
        return new(subtotal, discount, service, taxes, Round(tip), total, paid, Round(total - paid));
    }
    public static decimal[] SplitEqual(decimal total, int guests) {
        if (guests is < 1 or > 100 || total < 0 || Round(total) != total) throw new ArgumentException("Choose 1–100 guests and a valid total.");
        var cents = checked((long)(total * 100));
        return Enumerable.Range(0, guests).Select(i => (cents / guests + (i < cents % guests ? 1 : 0)) / 100m).ToArray();
    }
}

public static class PosSchema {
    public static void Configure(ModelBuilder m, RestaurantDb db) {
        m.Entity<Branch>();
        m.Entity<StaffBranch>().HasKey(x => new { x.UserId, x.BranchId });
        m.Entity<StaffBranch>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        m.Entity<StaffBranch>().HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId);
        m.Entity<PosSettings>().HasIndex(x => x.BranchId).IsUnique();
        m.Entity<PaymentEntry>().HasOne<TableSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PaymentEntry>().HasOne<PaymentMethod>().WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<PaymentEntry>().HasIndex(x => x.ProviderPaymentId).IsUnique().HasFilter("\"ProviderPaymentId\" <> ''");
        m.Entity<PaymentEntry>().ToTable("Payments", t => t.HasCheckConstraint("CK_Payment_Positive", "\"Amount\" > 0"));
        m.Entity<AuditEntry>(); m.Entity<GuestRequest>();
        m.Entity<GuestFeedback>().HasIndex(x => x.SessionId).IsUnique();
        m.Entity<DeviceToken>().HasIndex(x => x.Token).IsUnique();
        m.Entity<OutboxMessage>().HasIndex(x => new { x.DeliveredAt, x.NextAttemptAt });
        m.Entity<GatewayOrder>().HasIndex(x => x.ProviderOrderId).IsUnique();
        m.Entity<DiningTable>().HasIndex(x => x.GuestToken).IsUnique();
        m.Entity<Category>().HasIndex(x => x.Name).IsUnique(false);
        m.Entity<Category>().HasIndex(x => new { x.BranchId, x.Name }).IsUnique();
        m.Entity<DiningTable>().HasIndex(x => x.Name).IsUnique(false);
        m.Entity<DiningTable>().HasIndex(x => new { x.BranchId, x.Name }).IsUnique();
        m.Entity<PaymentMethod>().HasIndex(x => x.Name).IsUnique(false);
        m.Entity<PaymentMethod>().HasIndex(x => new { x.BranchId, x.Name }).IsUnique();
        // Query filters cover reads; SaveChanges below enforces branch ownership on writes.
        foreach (var type in m.Model.GetEntityTypes().Where(e => typeof(BranchRecord).IsAssignableFrom(e.ClrType)).ToList()) {
            var method = typeof(PosSchema).GetMethod(nameof(Filter))!.MakeGenericMethod(type.ClrType);
            method.Invoke(null, [m, db]);
        }
    }
    public static void Filter<T>(ModelBuilder m, RestaurantDb db) where T : BranchRecord => m.Entity<T>().HasQueryFilter(x => x.BranchId == db.CurrentBranchId);
}
