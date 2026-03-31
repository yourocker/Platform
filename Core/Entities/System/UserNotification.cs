using System;
using Core.MultiTenancy;

namespace Core.Entities.System
{
    public class UserNotification : ITenantEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid? SourceEventId { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }
}
