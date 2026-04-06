using System.ComponentModel.DataAnnotations;
using Core.MultiTenancy;

namespace Core.Entities.CRM
{
    public class CrmSettings : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public bool UseLeads { get; set; } = true;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
