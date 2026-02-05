using Core.Entities.Platform; // Ссылка на основную платформу только для FK
using Core.FormEngine.Schema;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.FormEngine.Domain
{
    /// <summary>
    /// Сущность хранения макетов форм.
    /// </summary>
    [Table("AppFormDefinitions")]
    public class AppFormDefinition
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Связь с "родительской" сущностью системы.
        /// </summary>
        public Guid AppDefinitionId { get; set; }
        
        // Навигационное свойство
        public virtual AppDefinition AppDefinition { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [MaxLength(50)]
        public string FormCode { get; set; }
        
        /// <summary>
        /// Тип формы (Создание, Редактирование, Просмотр)
        /// </summary>
        public FormType Type { get; set; } = FormType.Edit; // Значение по умолчанию

        public bool IsDefault { get; set; }

        /// <summary>
        /// Хранилище структуры в формате JSONB
        /// </summary>
        [Column(TypeName = "jsonb")]
        public FormLayoutSchema Layout { get; set; } = new();

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}