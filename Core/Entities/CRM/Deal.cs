using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.CRM
{
    [Table("CrmDeals")]
    public class Deal : CrmBaseProcessEntity 
    {
        public Guid? SourceLeadId { get; set; }
        
        // Корзина услуг
        public virtual List<CrmDealItem> Items { get; set; } = new();
    }
}