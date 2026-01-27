using System.ComponentModel.DataAnnotations.Schema;
using Core.Entities.Platform;
using Core.Entities.Company;

namespace Core.Entities.CRM
{
    /// <summary>
    /// Базовый класс для сущностей CRM, участвующих в процессах (Лид, Сделка).
    /// Наследуется от GenericObject для поддержки динамических полей.
    /// </summary>
    public abstract class CrmBaseProcessEntity : GenericObject
    {
        public Guid PipelineId { get; set; }
        
        [ForeignKey(nameof(PipelineId))]
        public virtual CrmPipeline? Pipeline { get; set; }

        public Guid StageId { get; set; }

        [ForeignKey(nameof(StageId))]
        public virtual CrmStage? CurrentStage { get; set; }

        public Guid? ResponsibleId { get; set; }

        [ForeignKey(nameof(ResponsibleId))]
        public virtual Employee? Responsible { get; set; }

        public Guid? ContactId { get; set; }

        [ForeignKey(nameof(ContactId))]
        public virtual Contact? Contact { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        public string Currency { get; set; } = "RUB";
        
        public DateTime StageChangedAt { get; set; } = DateTime.UtcNow;
    }
}