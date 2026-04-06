using System.ComponentModel.DataAnnotations.Schema;
using Core.Entities.Platform;

namespace Core.Entities.CRM
{
    [Table("CrmCompanies")]
    public class CrmCompany : GenericObject
    {
        public virtual ICollection<CrmCompanyContact> ContactLinks { get; set; } = new List<CrmCompanyContact>();
        public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
        public virtual ICollection<Deal> Deals { get; set; } = new List<Deal>();
    }
}
