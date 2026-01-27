using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.CRM
{
    [Table("CrmLeads")]
    public class Lead : CrmBaseProcessEntity 
    {
        public bool IsConverted { get; set; }
        public Guid? ConvertedDealId { get; set; }
    }
}