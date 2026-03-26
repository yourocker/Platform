using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace CRM.Modules.Notifications.Infrastructure
{
    public class NameIdentifierUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
    }
}
