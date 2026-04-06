using System.ComponentModel.DataAnnotations.Schema;
using Core.MultiTenancy;

namespace Core.Entities.CRM
{
    [Table("CrmLeadContacts")]
    public class CrmLeadContact : ITenantEntity
    {
        public Guid TenantId { get; set; }

        public Guid LeadId { get; set; }

        [ForeignKey(nameof(LeadId))]
        public virtual Lead Lead { get; set; } = null!;

        public Guid ContactId { get; set; }

        [ForeignKey(nameof(ContactId))]
        public virtual Contact Contact { get; set; } = null!;

        public bool IsPrimary { get; set; }
    }
}
