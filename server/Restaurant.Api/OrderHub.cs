using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace Restaurant.Api;
[Authorize]
public class OrderHub:Hub {
 public override async Task OnConnectedAsync(){ var branch=Context.User?.FindFirst("branch")?.Value??"1"; await Groups.AddToGroupAsync(Context.ConnectionId,$"branch:{branch}");await Groups.AddToGroupAsync(Context.ConnectionId,$"branch:{branch}:user:{Context.UserIdentifier}"); await base.OnConnectedAsync(); }
}
