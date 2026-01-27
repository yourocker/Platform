using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.CRM
{
    /// <summary>
    /// Описание этапа воронки. 
    /// Содержит настройки цвета и список обязательных полей для перехода.
    /// </summary>
    public class CrmStage
    {
        [Key]
        public Guid Id { get; set; }

        public Guid PipelineId { get; set; }

        [ForeignKey(nameof(PipelineId))]
        public virtual CrmPipeline Pipeline { get; set; } = null!;

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Color { get; set; } = "#6c757d";

        public int SortOrder { get; set; }

        /// <summary>
        /// Тип этапа: 0 - В работе, 1 - Успех, 2 - Провал.
        /// </summary>
        public int StageType { get; set; }

        /// <summary>
        /// JSON-массив ID полей (из AppFieldDefinition), обязательных для заполнения на этом этапе.
        /// </summary>
        public string? RequiredFieldIdsJson { get; set; }
    }
}