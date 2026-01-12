using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Entities.Company;
using Core.Entities.Platform;

namespace Core.Entities.Tasks
{
    /// <summary>
    /// Сущность задачи сотрудника
    /// </summary>
    public class EmployeeTask : GenericObject
    {
        /// <summary>
        /// Название задачи
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Описание (суть) задачи
        /// </summary>
        [Column(TypeName = "text")]
        public string? Description { get; set; }

        /// <summary>
        /// Дата и время постановки задачи
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Срок выполнения (дедлайн)
        /// </summary>
        public DateTime? Deadline { get; set; }

        /// <summary>
        /// Идентификатор автора задачи (постановщика)
        /// </summary>
        public Guid AuthorId { get; set; }
        
        [ForeignKey("AuthorId")]
        public virtual Employee Author { get; set; }

        /// <summary>
        /// Идентификатор исполнителя задачи
        /// </summary>
        public Guid AssigneeId { get; set; }
        
        [ForeignKey("AssigneeId")]
        public virtual Employee Assignee { get; set; }

        /// <summary>
        /// Текущий статус задачи
        /// </summary>
        public TaskStatus Status { get; set; } = TaskStatus.Created;

        /// <summary>
        /// Флаг мягкого удаления
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// Дата мягкого удаления
        /// </summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>
        /// Множественные связи с объектами системы (Пациенты, Оборудование и т.д.)
        /// </summary>
        public virtual ICollection<TaskEntityRelation> Relations { get; set; } = new List<TaskEntityRelation>();
        
        public virtual ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    }
}