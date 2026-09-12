using Microsoft.EntityFrameworkCore;
namespace Restaurant.Api;
public class User { public string PinHash {get;set;}=""; public bool Owner {get;set;}=false; public Guid Id {get;set;} = Guid.NewGuid(); public string Name {get;set;}=""; public string Email {get;set;}=""; public string PasswordHash {get;set;}=""; public string Role {get;set;}="Waiter"; public bool Active {get;set;}=true; }
public class Category : BranchRecord { public int Id {get;set;} public string Name {get;set;}=""; }
public class MenuItem : BranchRecord { public bool Available {get;set;}=true; public int? Stock {get;set;} public string Station {get;set;}="Kitchen"; public string PortionsJson {get;set;}="[]"; public string ModifiersJson {get;set;}="[]"; public string PhotoUrl {get;set;}=""; public Guid? CatalogId {get;set;} public int Id {get;set;} public string Name {get;set;}=""; public string Description {get;set;}=""; public int CategoryId {get;set;} public decimal Price {get;set;} public bool Active {get;set;}=true; public bool Vegetarian {get;set;}=true; }
public class DiningTable : BranchRecord { public string Section {get;set;}="Main Hall"; public int X {get;set;} public int Y {get;set;} public string GuestToken {get;set;}=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)); public int Id {get;set;} public string Name {get;set;}=""; public int Seats {get;set;}=4; public bool Active {get;set;}=true; }
public class PaymentMethod : BranchRecord { public int Id {get;set;} public string Name {get;set;}=""; public bool Active {get;set;}=true; }
public class TableSession : BranchRecord { public string DiscountKind {get;set;}="None"; public decimal DiscountValue {get;set;} public decimal Tip {get;set;} public string QuoteJson {get;set;}=""; public string SplitJson {get;set;}="[]"; public Guid Id {get;set;}=Guid.NewGuid(); public int TableId {get;set;} public DateTime OpenedAt {get;set;}=DateTime.UtcNow; public DateTime? ClosedAt {get;set;} }
public class Order : BranchRecord { public Guid Id {get;set;}=Guid.NewGuid(); public Guid SessionId {get;set;} public int TableId {get;set;} public Guid WaiterId {get;set;} public string Status {get;set;}="New"; public string Instructions {get;set;}=""; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public DateTime UpdatedAt {get;set;}=DateTime.UtcNow; public List<OrderItem> Items {get;set;}=[]; }
public class OrderItem : BranchRecord { public string Station {get;set;}="Kitchen"; public string Portion {get;set;}=""; public string ModifiersJson {get;set;}="[]"; public Guid Id {get;set;}=Guid.NewGuid(); public Guid OrderId {get;set;} public int MenuItemId {get;set;} public string Name {get;set;}=""; public int Quantity {get;set;} public decimal UnitPrice {get;set;} }
public class Bill : BranchRecord { public string QuoteJson {get;set;}=""; public Guid Id {get;set;}=Guid.NewGuid(); public Guid SessionId {get;set;} public int TableId {get;set;} public Guid CashierId {get;set;} public decimal Total {get;set;} public int PaymentMethodId {get;set;} public string Reference {get;set;}=""; public DateTime PaidAt {get;set;}=DateTime.UtcNow; }
public class Notification : BranchRecord { public Guid Id {get;set;}=Guid.NewGuid(); public Guid UserId {get;set;} public Guid OrderId {get;set;} public string Message {get;set;}=""; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public bool Read {get;set;} }
public class OrderEvent : BranchRecord { public Guid Id {get;set;}=Guid.NewGuid(); public Guid OrderId {get;set;} public Guid UserId {get;set;} public string Status {get;set;}=""; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; }
public class RestaurantDb(DbContextOptions<RestaurantDb> options, IHttpContextAccessor? accessor=null):DbContext(options) {
 public int CurrentBranchId {get;private set;}=int.TryParse(accessor?.HttpContext?.User.FindFirst("branch")?.Value,out var branch)?branch:1;
 public void UseBranch(int id)=>CurrentBranchId=id;
 public DbSet<Branch> Branches=>Set<Branch>(); public DbSet<StaffBranch> StaffBranches=>Set<StaffBranch>();
 public DbSet<PosSettings> Settings=>Set<PosSettings>(); public DbSet<PaymentEntry> Payments=>Set<PaymentEntry>(); public DbSet<AuditEntry> Audit=>Set<AuditEntry>(); public DbSet<DeviceToken> Devices=>Set<DeviceToken>(); public DbSet<OutboxMessage> Outbox=>Set<OutboxMessage>(); public DbSet<GuestRequest> GuestRequests=>Set<GuestRequest>(); public DbSet<GuestFeedback> Feedback=>Set<GuestFeedback>(); public DbSet<GatewayOrder> GatewayOrders=>Set<GatewayOrder>();
 public override Task<int> SaveChangesAsync(CancellationToken cancellationToken=default) {
  var changed=ChangeTracker.Entries<BranchRecord>().Where(e=>e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
  foreach(var entry in changed){if(entry.State==EntityState.Added)entry.Entity.BranchId=CurrentBranchId;else if(entry.Entity.BranchId!=CurrentBranchId||entry.Property(nameof(BranchRecord.BranchId)).IsModified)throw new InvalidOperationException("Cross-branch write rejected.");}
  if(changed.Any(e=>e.Entity is not OutboxMessage and not DeviceToken))Outbox.Add(new(){BranchId=CurrentBranchId});
  return base.SaveChangesAsync(cancellationToken);
 }

 public DbSet<User> Users=>Set<User>(); public DbSet<Category> Categories=>Set<Category>(); public DbSet<MenuItem> MenuItems=>Set<MenuItem>(); public DbSet<DiningTable> Tables=>Set<DiningTable>(); public DbSet<PaymentMethod> PaymentMethods=>Set<PaymentMethod>(); public DbSet<TableSession> Sessions=>Set<TableSession>(); public DbSet<Order> Orders=>Set<Order>(); public DbSet<OrderItem> OrderItems=>Set<OrderItem>(); public DbSet<Bill> Bills=>Set<Bill>(); public DbSet<Notification> Notifications=>Set<Notification>(); public DbSet<OrderEvent> OrderEvents=>Set<OrderEvent>();
 protected override void OnModelCreating(ModelBuilder m) {
  m.Entity<User>().HasIndex(x=>x.Email).IsUnique(); m.Entity<Category>().HasIndex(x=>x.Name).IsUnique(); m.Entity<DiningTable>().HasIndex(x=>x.Name).IsUnique(); m.Entity<PaymentMethod>().HasIndex(x=>x.Name).IsUnique();
  m.Entity<TableSession>().HasIndex(x=>x.TableId).IsUnique().HasFilter("\"ClosedAt\" IS NULL"); m.Entity<Bill>().HasIndex(x=>x.SessionId).IsUnique();
  m.Entity<Order>().HasMany(x=>x.Items).WithOne().HasForeignKey(x=>x.OrderId); m.Entity<Order>().HasIndex(x=>new{x.Status,x.CreatedAt});
  m.Entity<MenuItem>().HasOne<Category>().WithMany().HasForeignKey(x=>x.CategoryId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<TableSession>().HasOne<DiningTable>().WithMany().HasForeignKey(x=>x.TableId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<Order>().HasOne<TableSession>().WithMany().HasForeignKey(x=>x.SessionId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<Order>().HasOne<User>().WithMany().HasForeignKey(x=>x.WaiterId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<OrderItem>().HasOne<MenuItem>().WithMany().HasForeignKey(x=>x.MenuItemId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<Bill>().HasOne<TableSession>().WithMany().HasForeignKey(x=>x.SessionId).OnDelete(DeleteBehavior.Restrict); m.Entity<Bill>().HasOne<PaymentMethod>().WithMany().HasForeignKey(x=>x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<Notification>().HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict); m.Entity<Notification>().HasOne<Order>().WithMany().HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<OrderEvent>().HasOne<Order>().WithMany().HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<Bill>().HasOne<User>().WithMany().HasForeignKey(x=>x.CashierId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<OrderEvent>().HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<Order>().HasOne<DiningTable>().WithMany().HasForeignKey(x=>x.TableId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<Notification>().HasIndex(x=>new{x.UserId,x.Read,x.CreatedAt});
  m.Entity<User>().ToTable("Users",t=>t.HasCheckConstraint("CK_User_Role","\"Role\" IN ('Waiter','Kitchen','Cashier','Admin')"));
  m.Entity<Order>().ToTable("Orders",t=>t.HasCheckConstraint("CK_Order_Status","\"Status\" IN ('New','Preparing','Ready','Served','Paid','Voided')"));
  m.Entity<OrderItem>().ToTable("OrderItems",t=>{t.HasCheckConstraint("CK_Item_Quantity","\"Quantity\" BETWEEN 1 AND 99");t.HasCheckConstraint("CK_Item_Price","\"UnitPrice\" >= 0");});
  m.Entity<MenuItem>().ToTable("MenuItems",t=>t.HasCheckConstraint("CK_Menu_Price","\"Price\" >= 0"));
  m.Entity<DiningTable>().ToTable("Tables",t=>t.HasCheckConstraint("CK_Table_Seats","\"Seats\" BETWEEN 1 AND 50"));
  PosSchema.Configure(m,this);
  foreach(var e in m.Model.GetEntityTypes()) foreach(var p in e.GetProperties()) if(p.ClrType==typeof(decimal)){p.SetPrecision(12);p.SetScale(2);}
 }
}
public record LoginRequest(string Email,string Password);
public record ItemRequest(int MenuItemId,int Quantity,string? Portion=null,List<SelectedModifier>? Modifiers=null);
public record OrderRequest(int TableId,List<ItemRequest> Items,string? Instructions,Guid? ClientRequestId=null);
public record StatusRequest(string Status);
public record PayRequest(int PaymentMethodId,string? Reference,decimal ExpectedTotal);
public record UserRequest(string Name,string Email,string Role,string? Password,bool Active);
public static class Workflow {
 public static bool SameOrder(Order order,OrderRequest request,Guid waiterId)=>order.WaiterId==waiterId&&order.TableId==request.TableId&&order.Instructions==(request.Instructions?.Trim()??"")&&order.Items.OrderBy(i=>i.MenuItemId).Select(i=>(i.MenuItemId,i.Quantity)).SequenceEqual(request.Items.GroupBy(i=>i.MenuItemId).OrderBy(g=>g.Key).Select(g=>(g.Key,g.Sum(i=>i.Quantity))));
 public static bool IsAcknowledged(string current,string target,string role,bool owner){string[] stages=["New","Preparing","Ready","Served","Paid"];var index=Array.IndexOf(stages,target);return index is >0 and <4&&CanTransition(stages[index-1],target,role,owner)&&Array.IndexOf(stages,current)>=index;}
 public static readonly string[] Roles=["Waiter","Kitchen","Cashier","Admin"];
 public static bool CanTransition(string from,string to,string role,bool owner)=>role=="Admin" ? (from,to) is ("New","Preparing") or ("Preparing","Ready") or ("Ready","Served") : role=="Kitchen" ? (from,to) is ("New","Preparing") or ("Preparing","Ready") : role=="Waiter" && owner && from=="Ready" && to=="Served";
}
