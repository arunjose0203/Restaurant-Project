using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Restaurant.Api;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder=WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json",optional:true).AddEnvironmentVariables();
var connection=builder.Configuration.GetConnectionString("Restaurant");
var key=builder.Configuration["Jwt:Key"]??"";
if(key.Length<32) throw new InvalidOperationException("Configure Jwt:Key with at least 32 random characters.");
if(string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Configure ConnectionStrings:Restaurant with your online PostgreSQL connection string.");
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<RestaurantDb>(o=>o.UseNpgsql(connection));
builder.Services.AddSingleton<IPasswordHasher<User>,PasswordHasher<User>>();
var signalR=builder.Services.AddSignalR();
if(!string.IsNullOrWhiteSpace(builder.Configuration["Redis:Connection"]))signalR.AddStackExchangeRedis(builder.Configuration["Redis:Connection"]!,o=>o.Configuration.ChannelPrefix=StackExchange.Redis.RedisChannel.Literal(builder.Configuration["Redis:Prefix"]??"tableflow"));
builder.Services.AddOpenTelemetry().ConfigureResource(r=>r.AddService("Tableflow.Api")).WithTracing(t=>{t.AddAspNetCoreInstrumentation(o=>o.RecordException=true).AddHttpClientInstrumentation().AddSource("Npgsql");if(!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))t.AddOtlpExporter();});
builder.Services.AddHttpClient();
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddResponseCompression(o=>{o.EnableForHttps=true;});
builder.Services.AddCors(o=>o.AddDefaultPolicy(p=>p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>()??[]).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>{
 o.TokenValidationParameters=new(){ValidateIssuer=true,ValidateAudience=true,ValidateIssuerSigningKey=true,ValidateLifetime=true,ValidIssuer=builder.Configuration["Jwt:Issuer"],ValidAudience=builder.Configuration["Jwt:Audience"],IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),ClockSkew=TimeSpan.FromSeconds(15)};
 o.Events=new(){OnMessageReceived=c=>{if(c.HttpContext.Request.Path.StartsWithSegments("/hubs/orders"))c.Token=c.Request.Query["access_token"];return Task.CompletedTask;},OnTokenValidated=async c=>{var db=c.HttpContext.RequestServices.GetRequiredService<RestaurantDb>();db.UseBranch(int.TryParse(c.Principal?.FindFirst("branch")?.Value,out var bid)?bid:1);var id=c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);var role=c.Principal?.FindFirstValue(ClaimTypes.Role);if(!Guid.TryParse(id,out var uid)||!await db.Users.AnyAsync(u=>u.Id==uid&&u.Active))c.Fail("Account is inactive.");else if(!await db.StaffBranches.AnyAsync(m=>m.UserId==uid&&m.BranchId==db.CurrentBranchId&&m.Role==role)||!await db.Branches.AnyAsync(b=>b.Id==db.CurrentBranchId&&b.Active))c.Fail("Branch access changed.");}};
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o=>o.AddPolicy("login",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new(){PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}))); 
var app=builder.Build();
app.Use(async(ctx,next)=>{try{await next();}catch(ArgumentException ex){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{message=ex.Message});}catch(System.Text.Json.JsonException){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{message="Invalid configuration JSON."});}catch(DbUpdateException){ctx.Response.StatusCode=409;await ctx.Response.WriteAsJsonAsync(new{message="This change conflicts with an existing record. Reload and try again."});}catch(Npgsql.PostgresException ex) when(ex.SqlState=="40001"||ex.SqlState=="40P01"){ctx.Response.StatusCode=409;await ctx.Response.WriteAsJsonAsync(new{message="Another staff member updated this table. Reload and try again."});}});
app.UseCors();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();
// Compress operational snapshots, never authentication/token responses.
app.UseWhen(ctx=>ctx.Request.Path=="/api/state"&&HttpMethods.IsGet(ctx.Request.Method),branch=>branch.UseResponseCompression());
// Schema changes are explicit deployment operations, never automatic on each startup.
if(args.Contains("--migrate")){using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<RestaurantDb>();await db.Database.MigrateAsync();await Seed.Run(db,scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(),builder.Configuration);return;}
app.MapGet("/health",()=>Results.Ok(new{status="ok",database="PostgreSQL"}));
app.MapIdentity();
var api=app.MapGroup("/api").RequireAuthorization();
api.MapGet("/state",async(RestaurantDb db,ClaimsPrincipal user)=>{
 var uid=UserId(user);var admin=user.IsInRole("Admin");var orders=db.Orders.Include(x=>x.Items).AsNoTracking().AsQueryable();
 if(user.IsInRole("Waiter"))orders=orders.Where(x=>x.WaiterId==uid);
 if(user.IsInRole("Kitchen"))orders=orders.Where(x=>x.Status!="Paid");
 return Results.Ok(new{categories=await db.Categories.OrderBy(x=>x.Id).ToListAsync(),menu=await db.MenuItems.OrderBy(x=>x.Id).ToListAsync(),tables=await db.Tables.OrderBy(x=>x.Id).ToListAsync(),paymentMethods=await db.PaymentMethods.ToListAsync(),sessions=await db.Sessions.Where(x=>x.ClosedAt==null).ToListAsync(),guestRequests=await db.GuestRequests.Where(r=>!r.Resolved).ToListAsync(),orders=await orders.Where(x=>x.Status!="Paid"||x.UpdatedAt>=DateTime.UtcNow.AddDays(-2)).OrderByDescending(x=>x.CreatedAt).ToListAsync(),notifications=await db.Notifications.Where(x=>x.UserId==uid&&!x.Read).OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync(),bills=admin||user.IsInRole("Cashier")?await db.Bills.Where(x=>x.PaidAt>=DateTime.UtcNow.AddDays(-2)).OrderByDescending(x=>x.PaidAt).ToListAsync():[],users=admin?await db.Users.Where(x=>db.StaffBranches.Any(m=>m.UserId==x.Id&&m.BranchId==db.CurrentBranchId)).Select(x=>new{x.Id,x.Name,x.Email,Role=db.StaffBranches.Where(m=>m.UserId==x.Id&&m.BranchId==db.CurrentBranchId).Select(m=>m.Role).First(),x.Active}).ToListAsync():null,roles=Workflow.Roles});
});
api.MapPost("/orders",async(OrderRequest r,RestaurantDb db,ClaimsPrincipal user)=>{await using var tx=await db.Database.BeginTransactionAsync();var order=await PosService.CreateOrder(db,r with {ClientRequestId=r.ClientRequestId??Guid.NewGuid()},UserId(user));await tx.CommitAsync();return Results.Ok(order);}).RequireAuthorization(p=>p.RequireRole("Waiter","Admin"));
api.MapPatch("/orders/{id:guid}/status",async(Guid id,StatusRequest r,RestaurantDb db,ClaimsPrincipal user,IHubContext<OrderHub> hub)=>{
 var tableId=await db.Orders.Where(x=>x.Id==id).Select(x=>(int?)x.TableId).SingleOrDefaultAsync();if(tableId==null)return Results.NotFound();
 await using var tx=await db.Database.BeginTransactionAsync();await db.Tables.FromSqlInterpolated($"SELECT * FROM \"Tables\" WHERE \"Id\" = {tableId.Value} FOR UPDATE").SingleAsync();
 var order=await db.Orders.SingleAsync(x=>x.Id==id);await PosService.LockSession(db,order.SessionId);var role=user.FindFirstValue(ClaimTypes.Role)??"";var owner=order.WaiterId==UserId(user);if(Workflow.IsAcknowledged(order.Status,r.Status,role,owner))return Results.Ok(order);if(!Workflow.CanTransition(order.Status,r.Status,role,owner))return Results.Conflict(new{message="This transition is not allowed. Reload to see the current status."});
 order.Status=r.Status;order.UpdatedAt=DateTime.UtcNow;db.OrderEvents.Add(new(){OrderId=id,UserId=UserId(user),Status=r.Status});
 Notification? notification=null;if(r.Status=="Ready"){notification=new(){UserId=order.WaiterId,OrderId=id,Message=$"Food ready · Table {order.TableId}"};db.Notifications.Add(notification);db.Outbox.Add(new(){Kind="FoodReady",UserId=order.WaiterId,Payload=System.Text.Json.JsonSerializer.Serialize(notification)});}
 await db.SaveChangesAsync();await tx.CommitAsync();await Changed(hub);if(notification!=null){try{await hub.Clients.User(order.WaiterId.ToString()).SendAsync("FoodReady",notification);}catch{/* Unread notification is durable and recovered by state synchronization. */}}return Results.Ok(order);
}).RequireAuthorization(p=>p.RequireRole("Kitchen","Waiter","Admin"));
api.MapPost("/notifications/{id:guid}/read",async(Guid id,RestaurantDb db,ClaimsPrincipal user)=>{var uid=UserId(user);await db.Notifications.Where(x=>x.Id==id&&x.UserId==uid).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Read,true));return Results.NoContent();});
api.MapGet("/sessions/{id:guid}/bill",async(Guid id,RestaurantDb db)=>{var s=await db.Sessions.FindAsync(id);if(s==null)return Results.NotFound();var orders=await db.Orders.Include(x=>x.Items).Where(x=>x.SessionId==id).ToListAsync();return Results.Ok(new{session=s,orders,total=(await PosService.Quote(db,s)).Total,canPay=s.ClosedAt==null&&orders.Count>0&&orders.All(x=>x.Status=="Served"||x.Status=="Voided"),payment=await db.Bills.SingleOrDefaultAsync(x=>x.SessionId==id)});}).RequireAuthorization(p=>p.RequireRole("Cashier","Admin"));
api.MapPost("/sessions/{id:guid}/pay",async(Guid id,PayRequest r,RestaurantDb db,ClaimsPrincipal user)=>{
 await using var tx=await db.Database.BeginTransactionAsync();var session=await PosService.LockSession(db,id);if(session==null)return Results.NotFound();var existing=await db.Bills.SingleOrDefaultAsync(b=>b.SessionId==id);if(existing!=null)return Results.Ok(existing);var quote=await PosService.Quote(db,session);await PosService.Pay(db,session,new(Guid.NewGuid(),r.PaymentMethodId,quote.Outstanding,r.ExpectedTotal,r.Reference),UserId(user));await tx.CommitAsync();return Results.Ok(await db.Bills.SingleAsync(b=>b.SessionId==id));
}).RequireAuthorization(p=>p.RequireRole("Cashier","Admin"));
var admin=api.MapGroup("/admin").RequireAuthorization(p=>p.RequireRole("Admin"));
admin.MapPut("/menu/{id:int}",async(int id,MenuItem r,RestaurantDb db,IHubContext<OrderHub> hub)=>{if(string.IsNullOrWhiteSpace(r.Name)||r.Name.Length>100||r.Price<0||r.Price>100000||!await db.Categories.AnyAsync(x=>x.Id==r.CategoryId))return Bad("Enter a name, category and valid price.");var row=id==0?new MenuItem():await db.MenuItems.FindAsync(id);if(row==null)return Results.NotFound();MenuConfiguration.Validate(r);row.Available=r.Available;row.Stock=r.Stock;row.Station=r.Station;row.PortionsJson=r.PortionsJson;row.ModifiersJson=r.ModifiersJson;row.PhotoUrl=r.PhotoUrl;row.Name=r.Name.Trim();row.Description=r.Description;row.Price=r.Price;row.Active=r.Active;row.CategoryId=r.CategoryId;row.Vegetarian=r.Vegetarian;if(id==0)db.MenuItems.Add(row);await db.SaveChangesAsync();await Changed(hub);return Results.Ok(row);});
admin.MapPut("/categories/{id:int}",async(int id,Category r,RestaurantDb db,IHubContext<OrderHub> hub)=>{if(string.IsNullOrWhiteSpace(r.Name)||r.Name.Length>80)return Bad("Enter a category name.");var row=id==0?new Category():await db.Categories.FindAsync(id);if(row==null)return Results.NotFound();row.Name=r.Name.Trim();if(id==0)db.Categories.Add(row);await db.SaveChangesAsync();await Changed(hub);return Results.Ok(row);});
admin.MapPut("/tables/{id:int}",async(int id,DiningTable r,RestaurantDb db,IHubContext<OrderHub> hub)=>{if(string.IsNullOrWhiteSpace(r.Name)||r.Seats<1||r.Seats>50)return Bad("Enter a table name and 1–50 seats.");var row=id==0?new DiningTable():await db.Tables.FindAsync(id);if(row==null)return Results.NotFound();if(!r.Active&&await db.Sessions.AnyAsync(x=>x.TableId==id&&x.ClosedAt==null))return Bad("Close the current visit before disabling this table.");if(r.X is <0 or >1000||r.Y is <0 or >1000||r.Section.Length>80)return Bad("Invalid floor position or section.");row.Section=r.Section;row.X=r.X;row.Y=r.Y;row.Name=r.Name.Trim();row.Seats=r.Seats;row.Active=r.Active;if(id==0)db.Tables.Add(row);await db.SaveChangesAsync();await Changed(hub);return Results.Ok(row);});
admin.MapPut("/paymentMethods/{id:int}",async(int id,PaymentMethod r,RestaurantDb db,IHubContext<OrderHub> hub)=>{if(string.IsNullOrWhiteSpace(r.Name)||r.Name.Length>80)return Bad("Enter a payment method name.");var row=id==0?new PaymentMethod():await db.PaymentMethods.FindAsync(id);if(row==null)return Results.NotFound();row.Name=r.Name.Trim();row.Active=r.Active;if(id==0)db.PaymentMethods.Add(row);await db.SaveChangesAsync();await Changed(hub);return Results.Ok(row);});
admin.MapPut("/users/{id:guid}",async(Guid id,UserRequest r,RestaurantDb db,ClaimsPrincipal actor,IPasswordHasher<User> hasher,IHubContext<OrderHub> hub)=>{if(id!=Guid.Empty&&!await db.StaffBranches.AnyAsync(m=>m.UserId==id&&m.BranchId==db.CurrentBranchId))return Results.NotFound();if(id!=Guid.Empty&&await db.Users.AnyAsync(u=>u.Id==id&&u.Owner)&&id!=UserId(actor))return Results.Forbid();if(string.IsNullOrWhiteSpace(r.Name)||!r.Email.Contains('@')||!Workflow.Roles.Contains(r.Role))return Bad("Enter a name, email and supported role.");if(id==UserId(actor)&&(!r.Active||r.Role!="Admin"))return Bad("You cannot disable or demote your own administrator account.");var row=id==Guid.Empty?new User():await db.Users.FindAsync(id);if(row==null)return Results.NotFound();if((id==Guid.Empty||!string.IsNullOrEmpty(r.Password))&&(r.Password?.Length??0)<12)return Bad("Use a password of at least 12 characters.");row.Name=r.Name.Trim();row.Email=r.Email.Trim().ToLower();row.Role=r.Role;row.Active=r.Active;if(!string.IsNullOrEmpty(r.Password))row.PasswordHash=hasher.HashPassword(row,r.Password);if(id==Guid.Empty){db.Users.Add(row);db.StaffBranches.Add(new(){UserId=row.Id,BranchId=db.CurrentBranchId,Role=row.Role});}else {var membership=await db.StaffBranches.SingleOrDefaultAsync(m=>m.UserId==id&&m.BranchId==db.CurrentBranchId);if(membership==null)return Results.NotFound();membership.Role=r.Role;}await db.SaveChangesAsync();await Changed(hub);return Results.Ok(new{row.Id,row.Name,row.Email,row.Role,row.Active});});
admin.MapGet("/sales",async(DateTimeOffset? from,DateTimeOffset? to,RestaurantDb db)=>{var start=(from??new DateTimeOffset(DateTime.UtcNow.Date)).UtcDateTime;var end=(to??new DateTimeOffset(start.AddDays(1))).UtcDateTime;if(end<=start||end-start>TimeSpan.FromDays(366))return Bad("Choose a valid range of up to one year.");var bills=await db.Bills.Where(x=>x.PaidAt>=start&&x.PaidAt<end).ToListAsync();return Results.Ok(new{from=start,to=end,total=bills.Sum(x=>x.Total),count=bills.Count,byMethod=bills.GroupBy(x=>x.PaymentMethodId).Select(g=>new{paymentMethodId=g.Key,total=g.Sum(x=>x.Total),count=g.Count()})});});
admin.MapGet("/history",async(int? page,RestaurantDb db)=>Results.Ok(await db.Orders.Include(x=>x.Items).OrderByDescending(x=>x.CreatedAt).Skip(Math.Max(0,(page??1)-1)*50).Take(50).ToListAsync()));
app.MapPos();
app.MapGuest();
app.MapIntegrations();
app.MapHub<OrderHub>("/hubs/orders",o=>o.CloseOnAuthenticationExpiration=true).RequireAuthorization();app.Run();
static Guid UserId(ClaimsPrincipal p)=>Guid.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
static IResult Bad(string message)=>Results.BadRequest(new{message});
static async Task Changed(IHubContext<OrderHub> hub){try{await Task.CompletedTask;}catch{/* Persisted state remains authoritative; clients resync on reconnect and periodically. */}}
public partial class Program;


