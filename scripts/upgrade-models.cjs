const fs=require('node:fs');
const file='server/Restaurant.Api/Models.cs';
let s=fs.readFileSync(file,'utf8');
for(const name of ['Category','MenuItem','DiningTable','PaymentMethod','TableSession','Order','OrderItem','Bill','Notification','OrderEvent'])s=s.replace(`public class ${name} {`,`public class ${name} : BranchRecord {`);
s=s.replace('public class User {','public class User { public string PinHash {get;set;}=""; public bool Owner {get;set;}=false;');
s=s.replace('public class MenuItem : BranchRecord {','public class MenuItem : BranchRecord { public bool Available {get;set;}=true; public int? Stock {get;set;} public string Station {get;set;}="Kitchen"; public string PortionsJson {get;set;}="[]"; public string ModifiersJson {get;set;}="[]"; public string PhotoUrl {get;set;}=""; public Guid? CatalogId {get;set;}');
s=s.replace('public class DiningTable : BranchRecord {','public class DiningTable : BranchRecord { public string Section {get;set;}="Main Hall"; public int X {get;set;} public int Y {get;set;} public string GuestToken {get;set;}=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));');
s=s.replace('public class TableSession : BranchRecord {','public class TableSession : BranchRecord { public string DiscountKind {get;set;}="None"; public decimal DiscountValue {get;set;} public decimal Tip {get;set;} public string QuoteJson {get;set;}=""; public string SplitJson {get;set;}="[]";');
s=s.replace('public class OrderItem : BranchRecord {','public class OrderItem : BranchRecord { public string Station {get;set;}="Kitchen"; public string Portion {get;set;}=""; public string ModifiersJson {get;set;}="[]";');
s=s.replace('public class Bill : BranchRecord {','public class Bill : BranchRecord { public string QuoteJson {get;set;}="";');
s=s.replace('public class RestaurantDb(DbContextOptions<RestaurantDb> options):DbContext(options) {',`public class RestaurantDb(DbContextOptions<RestaurantDb> options, IHttpContextAccessor? accessor=null):DbContext(options) {
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
`);
s=s.replace("'Served','Paid')", "'Served','Paid','Voided')");
s=s.replace('  foreach(var e in m.Model.GetEntityTypes())','  PosSchema.Configure(m,this);\n  foreach(var e in m.Model.GetEntityTypes())');
s=s.replace('public record ItemRequest(int MenuItemId,int Quantity);','public record ItemRequest(int MenuItemId,int Quantity,string? Portion=null,List<SelectedModifier>? Modifiers=null);');
fs.writeFileSync(file,s);
