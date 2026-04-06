using System.ComponentModel.DataAnnotations.Schema;
using Core.MultiTenancy;

namespace Core.Entities.CRM
{
    [Table("CrmDealContacts")]
    public class CrmDealContact : ITenantEntity
    {
        public Guid TenantId { get; set; }

        public Guid DealId { get; set; }

        [ForeignKey(nameof(DealId))]
        public virtual Deal Deal { get; set; } = null!;

        public Guid ContactId { get; set; }

        [ForeignKey(nameof(ContactId))]
        public virtual Contact Contact { get; set; } = null!;

        public bool IsPrimary { get; set; }
    }
}
