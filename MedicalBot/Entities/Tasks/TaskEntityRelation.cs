using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalBot.Entities.Tasks
{
    /// <summary>
    /// Промежуточная сущность для связи задачи с объектами системы (Многие-ко-многим)
    /// </summary>
    public class TaskEntityRelation
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Идентификатор задачи
        /// </summary>
        public Guid TaskId { get; set; }
        
        [ForeignKey("TaskId")]
        public virtual EmployeeTask Task { get; set; } = null!;

        /// <summary>
        /// Код типа сущности (например, "Patient", "Equipment")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string EntityCode { get; set; } = string.Empty;

        /// <summary>
        /// Идентификатор конкретной записи объекта
        /// </summary>
        public Guid EntityId { get; set; }

        /// <summary>
        /// Кэшированное имя объекта для быстрого вывода в списках (необязательно)
        /// </summary>
        public string? EntityName { get; set; }
    }
}