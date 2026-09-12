using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace ticketing_system_backend.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // The UserIdentifier is automatically mapped to the ClaimTypes.NameIdentifier (UserId) claim
            await base.OnConnectedAsync();
        }
    }
}
