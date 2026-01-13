using Microsoft.AspNetCore.SignalR;

namespace Notifications.Infrastructure
{
    public class QueryStringUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            return connection.GetHttpContext()?.Request.Query["userId"];
        }
    }
}