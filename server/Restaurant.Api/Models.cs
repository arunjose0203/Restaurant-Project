using Microsoft.EntityFrameworkCore;
namespace Restaurant.Api;
public class User { public Guid Id {get;set;} = Guid.NewGuid(); public string Name {get;set;}=""; public string Email {get;set;}=""; public string PasswordHash {get;set;}=""; public string Role {get;set;}="Waiter"; public bool Active {get;set;}=true; }
public class Category { public int Id {get;set;} public string Name {get;set;}=""; }
public class MenuItem { public int Id {get;set;} public string Name {get;set;}=""; public string Description {get;set;}=""; public int CategoryId {get;set;} public decimal Price {get;set;} public bool Active {get;set;}=true; public bool Vegetarian {get;set;}=true; }
public class DiningTable { public int Id {get;set;} public string Name {get;set;}=""; public int Seats {get;set;}=4; public bool Active {get;set;}=true; }
public class PaymentMethod { public int Id {get;set;} public string Name {get;set;}=""; public bool Active {get;set;}=true; }
public class TableSession { public Guid Id {get;set;}=Guid.NewGuid(); public int TableId {get;set;} public DateTime OpenedAt {get;set;}=DateTime.UtcNow; public DateTime? ClosedAt {get;set;} }
public class Order { public Guid Id {get;set;}=Guid.NewGuid(); public Guid SessionId {get;set;} public int TableId {get;set;} public Guid WaiterId {get;set;} public string Status {get;set;}="New"; public string Instructions {get;set;}=""; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public DateTime UpdatedAt {get;set;}=DateTime.UtcNow; public List<OrderItem> Items {get;set;}=[]; }
public class OrderItem { public Guid Id {get;set;}=Guid.NewGuid(); public Guid OrderId {get;set;} public int MenuItemId {get;set;} public string Name {get;set;}=""; public int Quantity {get;set;} public decimal UnitPrice {get;set;} }
public class Bill { public Guid Id {get;set;}=Guid.NewGuid(); public Guid SessionId {get;set;} public int TableId {get;set;} public Guid CashierId {get;set;} public decimal Total {get;set;} public int PaymentMethodId {get;set;} public string Reference {get;set;}=""; public DateTime PaidAt {get;set;}=DateTime.UtcNow; }
public class Notification { public Guid Id {get;set;}=Guid.NewGuid(); public Guid UserId {get;set;} public Guid OrderId {get;set;} public string Message {get;set;}=""; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; public bool Read {get;set;} }
public class OrderEvent { public Guid Id {get;set;}=Guid.NewGuid(); public Guid OrderId {get;set;} public Guid UserId {get;set;} public string Status {get;set;}=""; public DateTime CreatedAt {get;set;}=DateTime.UtcNow; }
public class RestaurantDb(DbContextOptions<RestaurantDb> options):DbContext(options) {
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
  m.Entity<Order>().ToTable("Orders",t=>t.HasCheckConstraint("CK_Order_Status","\"Status\" IN ('New','Preparing','Ready','Served','Paid')"));
  m.Entity<OrderItem>().ToTable("OrderItems",t=>{t.HasCheckConstraint("CK_Item_Quantity","\"Quantity\" BETWEEN 1 AND 99");t.HasCheckConstraint("CK_Item_Price","\"UnitPrice\" >= 0");});
  m.Entity<MenuItem>().ToTable("MenuItems",t=>t.HasCheckConstraint("CK_Menu_Price","\"Price\" >= 0"));
  m.Entity<DiningTable>().ToTable("Tables",t=>t.HasCheckConstraint("CK_Table_Seats","\"Seats\" BETWEEN 1 AND 50"));
  foreach(var e in m.Model.GetEntityTypes()) foreach(var p in e.GetProperties()) if(p.ClrType==typeof(decimal)){p.SetPrecision(12);p.SetScale(2);}
 }
}
public record LoginRequest(string Email,string Password);
public record ItemRequest(int MenuItemId,int Quantity);
public record OrderRequest(int TableId,List<ItemRequest> Items,string? Instructions);
public record StatusRequest(string Status);
public record PayRequest(int PaymentMethodId,string? Reference,decimal ExpectedTotal);
public record UserRequest(string Name,string Email,string Role,string? Password,bool Active);
public static class Workflow {
 public static readonly string[] Roles=["Waiter","Kitchen","Cashier","Admin"];
 public static bool CanTransition(string from,string to,string role,bool owner)=>role=="Admin" ? (from,to) is ("New","Preparing") or ("Preparing","Ready") or ("Ready","Served") : role=="Kitchen" ? (from,to) is ("New","Preparing") or ("Preparing","Ready") : role=="Waiter" && owner && from=="Ready" && to=="Served";
}
