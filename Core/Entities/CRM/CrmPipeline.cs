using System.ComponentModel.DataAnnotations;
using Core.MultiTenancy;

namespace Core.Entities.CRM
{
    /// <summary>
    /// Описание воронки продаж для Лидов или Сделок.
    /// Позволяет разделять потоки клиентов по разным бизнес-направлениям.
    /// </summary>
    public class CrmPipeline : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Системный код сущности (Lead или Deal).
        /// </summary>
        [Required]
        public string TargetEntityCode { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; }

        public virtual List<CrmStage> Stages { get; set; } = new();
    }
}
