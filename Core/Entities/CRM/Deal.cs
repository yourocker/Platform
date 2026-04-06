using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.CRM
{
    [Table("CrmDeals")]
    public class Deal : CrmBaseProcessEntity 
    {
        public Guid? SourceLeadId { get; set; }

        [ForeignKey(nameof(SourceLeadId))]
        public virtual Lead? SourceLead { get; set; }

        public Guid? CompanyId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public virtual CrmCompany? Company { get; set; }
        
        // Корзина услуг
        public virtual List<CrmDealItem> Items { get; set; } = new();
        public virtual List<CrmDealContact> ContactLinks { get; set; } = new();
    }
}
