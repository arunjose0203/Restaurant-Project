using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
namespace Restaurant.Api;
public class OutboxWorker(IServiceScopeFactory scopes, ILogger<OutboxWorker> log, IConfiguration config) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stop) {
        while (!stop.IsCancellationRequested) {
            try {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<RestaurantDb>();
                await using var tx = await db.Database.BeginTransactionAsync(stop);
                // SKIP LOCKED permits multiple workers without simultaneous delivery of one event.
                var message = await db.Outbox.FromSqlRaw("SELECT * FROM \"Outbox\" WHERE \"DeliveredAt\" IS NULL AND \"NextAttemptAt\" <= now() ORDER BY \"CreatedAt\" LIMIT 1 FOR UPDATE SKIP LOCKED").IgnoreQueryFilters().FirstOrDefaultAsync(stop);
                if (message is null) { await tx.CommitAsync(stop); await Task.Delay(1000, stop); continue; }
                db.UseBranch(message.BranchId);
                try {
                    var hub = scope.ServiceProvider.GetRequiredService<IHubContext<OrderHub>>();
                    await hub.Clients.Group($"branch:{message.BranchId}").SendAsync("StateChanged", cancellationToken: stop);
                    if (message.Kind == "FoodReady" && message.UserId is Guid uid) {
                        var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload);
                        await hub.Clients.Group($"branch:{message.BranchId}:user:{uid}").SendAsync("FoodReady", payload, stop);
                        var devices = await db.Devices.Where(d => d.UserId == uid).ToListAsync(stop);
                        foreach (var device in devices) {
                            if (config["Push:Enabled"] != "true") continue;
                            var client = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
                            if (!string.IsNullOrEmpty(config["Push:AccessToken"])) client.DefaultRequestHeaders.Authorization = new("Bearer", config["Push:AccessToken"]);
                            using var response = await client.PostAsJsonAsync("https://exp.host/--/api/v2/push/send", new { to = device.Token, title = "Food ready", body = "An order is ready for collection.", sound = "default", channelId = "orders", data = new { eventId = message.Id } }, stop);
                            response.EnsureSuccessStatusCode();
                            var result = await response.Content.ReadFromJsonAsync<JsonElement>(stop);
                            if (result.TryGetProperty("data", out var data) && data.TryGetProperty("status", out var status) && status.GetString() == "error") {
                                if (data.TryGetProperty("details", out var details) && details.TryGetProperty("error", out var error) && error.GetString() == "DeviceNotRegistered") db.Devices.Remove(device);
                                else throw new InvalidOperationException("Push provider rejected a notification.");
                            }
                        }
                    }
                    message.DeliveredAt = DateTime.UtcNow; message.LastError = "";
                } catch (Exception ex) when (ex is not OperationCanceledException) { message.Attempts++; message.LastError = ex.GetType().Name; message.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(3600, Math.Pow(2, Math.Min(message.Attempts, 11)))); log.LogWarning("Outbox event {Id} failed on attempt {Attempt}", message.Id, message.Attempts); }
                await db.SaveChangesAsync(stop); await tx.CommitAsync(stop);
            } catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
            catch (Exception ex) { log.LogError(ex, "Outbox worker failed"); await Task.Delay(5000, stop); }
        }
    }
}
