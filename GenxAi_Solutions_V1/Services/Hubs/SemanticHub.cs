using Microsoft.AspNetCore.SignalR;

namespace GenxAi_Solutions_V1.Services.Hubs
{
    public class SemanticHub : Hub
    {
        public Task JoinCompanyGroup(string group)
            => Groups.AddToGroupAsync(Context.ConnectionId, group);


    }
}
