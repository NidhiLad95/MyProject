using Microsoft.AspNetCore.SignalR;

namespace GenxAi_Solutions.Services.Hubs
{
    public class SemanticHub : Hub
    {
        public Task JoinCompanyGroup(string group)
            => Groups.AddToGroupAsync(Context.ConnectionId, group);


    }
}
