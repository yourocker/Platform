using System.ComponentModel.DataAnnotations.Schema;
using Core.MultiTenancy;

namespace Core.Entities.CRM
{
    [Table("CrmCompanyContacts")]
    public class CrmCompanyContact : ITenantEntity
    {
        public Guid TenantId { get; set; }

        public Guid CompanyId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public virtual CrmCompany Company { get; set; } = null!;

        public Guid ContactId { get; set; }

        [ForeignKey(nameof(ContactId))]
        public virtual Contact Contact { get; set; } = null!;

        public bool IsPrimary { get; set; }
    }
}
