using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CRM.Modules.Notifications.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
    }
}
