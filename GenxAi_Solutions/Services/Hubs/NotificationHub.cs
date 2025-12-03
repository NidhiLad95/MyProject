using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace GenxAi_Solutions.Services.Hubs
{
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userIdStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var companyIdStr = Context.User?.FindFirst("companyId")?.Value;

            if (!string.IsNullOrWhiteSpace(companyIdStr))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"company_{companyIdStr}");

            if (!string.IsNullOrWhiteSpace(userIdStr))
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userIdStr}");

            await base.OnConnectedAsync();
        }
    }
}
