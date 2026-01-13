using Microsoft.AspNetCore.SignalR;

namespace Notifications.Hubs
{
    public class NotificationHub : Hub
    {
        // Метод вызывается при подключении клиента
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        // Метод вызывается при отключении
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}