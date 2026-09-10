using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace Restaurant.Api;
[Authorize]
public class OrderHub:Hub {
 public override async Task OnConnectedAsync(){ var role=Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value; if(role!=null) await Groups.AddToGroupAsync(Context.ConnectionId,role); await base.OnConnectedAsync(); }
}
