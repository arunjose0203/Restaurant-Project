using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Restaurant.Api;
public static class IdentityEndpoints {
    public static object Session(User user, StaffBranch membership, IConfiguration config) {
        var expires = DateTime.UtcNow.AddHours(8);
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Role, membership.Role), new Claim("branch", membership.BranchId.ToString()) };
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims, expires: expires, signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        return new { token = new JwtSecurityTokenHandler().WriteToken(token), expires, user = new { user.Id, user.Name, user.Email, role = membership.Role, user.Active, branchId = membership.BranchId, user.Owner } };
    }
    public static void MapIdentity(this WebApplication app) {
        app.MapPost("/api/auth/login", async (LoginRequest r, RestaurantDb db, IPasswordHasher<User> hasher, IConfiguration config) => {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == r.Email.Trim().ToLower() && u.Active);
            if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, r.Password) == PasswordVerificationResult.Failed) return Results.Unauthorized();
            var membership = await db.StaffBranches.Where(m => m.UserId == user.Id && db.Branches.Any(b => b.Id == m.BranchId && b.Active)).OrderBy(m => m.BranchId).FirstOrDefaultAsync();
            return membership is null ? Results.Unauthorized() : Results.Ok(Session(user, membership, config));
        }).RequireRateLimiting("login");
        var auth = app.MapGroup("/api/auth").RequireAuthorization();
        auth.MapGet("/branches", async (RestaurantDb db, ClaimsPrincipal actor) => { var id = PosService.Actor(actor); return await db.StaffBranches.Where(m => m.UserId == id).Join(db.Branches.Where(b => b.Active), m => m.BranchId, b => b.Id, (m, b) => new { b.Id, b.Name, m.Role, b.TimeZone }).ToListAsync(); });
        auth.MapPost("/branch/{id:int}", async (int id, RestaurantDb db, ClaimsPrincipal actor, IConfiguration config) => {
            var user = await db.Users.SingleAsync(u => u.Id == PosService.Actor(actor));
            var membership = await db.StaffBranches.SingleOrDefaultAsync(m => m.UserId == user.Id && m.BranchId == id);
            return membership is null || !await db.Branches.AnyAsync(b => b.Id == id && b.Active) ? Results.Forbid() : Results.Ok(Session(user, membership, config));
        });
        auth.MapPost("/pin", async (PinRequest r, RestaurantDb db, ClaimsPrincipal actor, IPasswordHasher<User> hasher) => {
            if (r.Pin.Length != 4 || !r.Pin.All(char.IsAsciiDigit)) throw new ArgumentException("PIN must have four digits.");
            var user = await db.Users.SingleAsync(u => u.Id == PosService.Actor(actor)); user.PinHash = hasher.HashPassword(user, r.Pin);
            PosService.Audit(db, user.Id, "PIN", user.Id.ToString(), "PIN changed"); await db.SaveChangesAsync(); return Results.Ok(new {ok=true});
        }).RequireRateLimiting("login");
        // PIN unlock is only available from an already authenticated shared station.
        auth.MapGet("/staff", async (RestaurantDb db) => await db.StaffBranches.Where(m => m.BranchId == db.CurrentBranchId).Join(db.Users.Where(u => u.Active && u.PinHash != ""), m => m.UserId, u => u.Id, (m, u) => new { u.Id, u.Name, m.Role }).ToListAsync());
        auth.MapPost("/pin-login", async (PinLoginRequest r, RestaurantDb db, IPasswordHasher<User> hasher, IConfiguration config) => {
            if (r.BranchId != db.CurrentBranchId) return Results.Forbid();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == r.UserId && u.Active);
            var member = await db.StaffBranches.SingleOrDefaultAsync(m => m.UserId == r.UserId && m.BranchId == db.CurrentBranchId);
            if (user is null || member is null || user.PinHash.Length == 0 || hasher.VerifyHashedPassword(user, user.PinHash, r.Pin) == PasswordVerificationResult.Failed) return Results.Unauthorized();
            PosService.Audit(db, user.Id, "PIN unlock", user.Id.ToString(), "Shared station"); await db.SaveChangesAsync(); return Results.Ok(Session(user, member, config));
        }).RequireRateLimiting("login");
        var owner = app.MapGroup("/api/owner").RequireAuthorization().AddEndpointFilter(async (context, next) => {
            var db = context.HttpContext.RequestServices.GetRequiredService<RestaurantDb>();
            return await db.Users.AnyAsync(u => u.Id == PosService.Actor(context.HttpContext.User) && u.Owner && u.Active) ? await next(context) : Results.Forbid();
        });
        owner.MapGet("/branches", async (RestaurantDb db) => await db.Branches.ToListAsync());
        owner.MapPost("/branches", async (Branch r, RestaurantDb db, ClaimsPrincipal actor) => {
            if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 100) throw new ArgumentException("Enter a branch name.");
            try { TimeZoneInfo.FindSystemTimeZoneById(r.TimeZone); } catch { throw new ArgumentException("Unknown time zone."); }
            await using var tx = await db.Database.BeginTransactionAsync();
            var branch = new Branch { Name = r.Name, TimeZone = r.TimeZone }; db.Branches.Add(branch); await db.SaveChangesAsync();
            db.StaffBranches.Add(new() { UserId = PosService.Actor(actor), BranchId = branch.Id, Role = "Admin" });
            await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(branch);
        });
        owner.MapPut("/branches/{id:int}/staff/{userId:guid}", async (int id, Guid userId, MembershipRequest r, RestaurantDb db, ClaimsPrincipal actor) => {
            if (!Workflow.Roles.Contains(r.Role) || !await db.Branches.AnyAsync(b => b.Id == id) || !await db.Users.AnyAsync(u => u.Id == userId)) throw new ArgumentException("Invalid branch, staff, or role.");
            if (userId == PosService.Actor(actor) && r.Role != "Admin") throw new ArgumentException("Cannot demote your own owner membership.");
            var membership = await db.StaffBranches.SingleOrDefaultAsync(m => m.UserId == userId && m.BranchId == id);
            if (membership is null) db.StaffBranches.Add(new() { BranchId = id, UserId = userId, Role = r.Role }); else membership.Role = r.Role;
            PosService.Audit(db, PosService.Actor(actor), "Membership", userId.ToString(), "Owner assigned branch access", new { id, r.Role }); await db.SaveChangesAsync(); return Results.Ok(new {ok=true});
        });
        owner.MapPost("/catalog/{itemId:int}/copy/{branchId:int}", async (int itemId, int branchId, RestaurantDb db, ClaimsPrincipal actor) => {
            var item = await db.MenuItems.SingleOrDefaultAsync(i => i.Id == itemId); if (item is null || !await db.Branches.AnyAsync(b => b.Id == branchId && b.Active)) return Results.NotFound();
            await using var tx = await db.Database.BeginTransactionAsync(); item.CatalogId ??= Guid.NewGuid(); await db.SaveChangesAsync();
            var categoryName = (await db.Categories.SingleAsync(c => c.Id == item.CategoryId)).Name;
            db.ChangeTracker.Clear(); db.UseBranch(branchId);
            var category = await db.Categories.SingleOrDefaultAsync(c => c.Name == categoryName); if (category is null) { category = new() { Name = categoryName }; db.Categories.Add(category); await db.SaveChangesAsync(); }
            var target = await db.MenuItems.SingleOrDefaultAsync(i => i.CatalogId == item.CatalogId);
            if (target is null) { target = new() { CatalogId = item.CatalogId, Price = item.Price, CategoryId = category.Id }; db.MenuItems.Add(target); }
            target.Name = item.Name; target.Description = item.Description; target.Vegetarian = item.Vegetarian; target.PhotoUrl = item.PhotoUrl; target.PortionsJson = item.PortionsJson; target.ModifiersJson = item.ModifiersJson; target.Station = item.Station;
            PosService.Audit(db, PosService.Actor(actor), "Catalog sync", target.CatalogId.ToString()!, "Shared item copied; existing local base price and stock preserved"); await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(target);
        });
    }
}
public record MembershipRequest(string Role);
