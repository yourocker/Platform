using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.CRM
{
    [Table("CrmLeads")]
    public class Lead : CrmBaseProcessEntity 
    {
        public Guid? CompanyId { get; set; }

        [ForeignKey(nameof(CompanyId))]
        public virtual CrmCompany? Company { get; set; }

        public bool IsConverted { get; set; }
        public DateTime? ConvertedAt { get; set; }

        public virtual List<CrmLeadContact> ContactLinks { get; set; } = new();

        [InverseProperty(nameof(Deal.SourceLead))]
        public virtual List<Deal> ConvertedDeals { get; set; } = new();
    }
}
