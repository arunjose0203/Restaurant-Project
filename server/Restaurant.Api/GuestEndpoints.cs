using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
namespace Restaurant.Api;
public static class GuestEndpoints {
    public static async Task<DiningTable?> Table(RestaurantDb db, string token) {
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return null;
        var table = await db.Tables.IgnoreQueryFilters().SingleOrDefaultAsync(t => t.GuestToken == token && t.Active);
        if (table is null || !await db.Branches.AnyAsync(b => b.Id == table.BranchId && b.Active)) return null;
        db.UseBranch(table.BranchId); return table;
    }
    public static void MapGuest(this WebApplication app) {
        var guest = app.MapGroup("/api/guest/{token}").RequireRateLimiting("login");
        guest.MapGet("", async (string token, RestaurantDb db) => {
            var table = await Table(db, token); if (table is null) return Results.NotFound();
            var settings = await PosService.Settings(db);
            var session = await db.Sessions.SingleOrDefaultAsync(s => s.TableId == table.Id && s.ClosedAt == null);
            return Results.Ok(new { table = new { table.Id, table.Name }, settings.BusinessName, settings.UpiId, menu = await db.MenuItems.Where(m => m.Active).ToListAsync(), categories = await db.Categories.ToListAsync(), sessionId = session?.Id, quote = session is null ? null : await PosService.Quote(db, session) });
        });
        guest.MapPost("/orders", async (string token, OrderRequest r, RestaurantDb db) => {
            var table = await Table(db, token); if (table is null) return Results.NotFound();
            if (r.TableId != table.Id) throw new ArgumentException("Table does not match the QR code.");
            await using var tx = await db.Database.BeginTransactionAsync();
            // Serialize guest identity creation on the table lock.
            await db.Tables.FromSqlInterpolated($"SELECT * FROM \"Tables\" WHERE \"Id\"={table.Id} FOR UPDATE").SingleAsync();
            var email = $"guest-table-{table.Id}@tableflow.invalid";
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
            if (user is null) { user = new() { Name = "QR guest", Email = email, Role = "Waiter", Active = false }; db.Users.Add(user); await db.SaveChangesAsync(); }
            var order = await PosService.CreateOrder(db, r, user.Id); await tx.CommitAsync();
            return Results.Ok(new { order.Id, order.Status, order.SessionId });
        });
        guest.MapPost("/request", async (string token, ServiceRequest r, RestaurantDb db) => {
            var table = await Table(db, token); if (table is null) return Results.NotFound();
            if (r.Kind is not ("Waiter" or "Bill")) throw new ArgumentException("Choose Waiter or Bill.");
            if (!await db.GuestRequests.AnyAsync(g => g.TableId == table.Id && !g.Resolved && g.Kind == r.Kind)) { db.GuestRequests.Add(new() { TableId = table.Id, Kind = r.Kind }); await db.SaveChangesAsync(); }
            return Results.Ok(new {ok=true});
        });
        guest.MapPost("/feedback", async (string token, FeedbackRequest r, RestaurantDb db) => {
            var table = await Table(db, token); if (table is null) return Results.NotFound();
            if (r.Rating is < 1 or > 5 || r.Comment.Length > 1000 || !await db.Bills.AnyAsync(b => b.SessionId == r.SessionId && b.TableId == table.Id)) throw new ArgumentException("Feedback requires a paid visit, 1–5 stars, and up to 1000 characters.");
            if (await db.Feedback.AnyAsync(f => f.SessionId == r.SessionId)) return Results.Ok(new {ok=true});
            db.Feedback.Add(new() { SessionId = r.SessionId, Rating = r.Rating, Comment = r.Comment }); await db.SaveChangesAsync(); return Results.Ok(new {ok=true});
        });
        app.MapPost("/api/pos/admin/tables/{id:int}/rotate-qr", async (int id, RestaurantDb db, System.Security.Claims.ClaimsPrincipal actor) => {
            var table = await db.Tables.SingleOrDefaultAsync(t => t.Id == id); if (table is null) return Results.NotFound();
            table.GuestToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); PosService.Audit(db, PosService.Actor(actor), "QR rotated", id.ToString(), "Old QR revoked"); await db.SaveChangesAsync(); return Results.Ok(new { table.GuestToken });
        }).RequireAuthorization(p => p.RequireRole("Admin"));
    }
}
public record ServiceRequest(string Kind);
public record FeedbackRequest(Guid SessionId, int Rating, string Comment);
