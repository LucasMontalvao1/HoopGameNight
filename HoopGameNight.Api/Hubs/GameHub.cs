using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace HoopGameNight.Api.Hubs
{
    public class GameHub : Hub
    {
        // Neste momento, o Hub é apenas um conduíte servidor -> cliente.
        // O cliente escuta o evento "ReceiveGameUpdates".
        
        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "DashboardGroup");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(System.Exception? exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "DashboardGroup");
            await base.OnDisconnectedAsync(exception);
        }
    }
}
