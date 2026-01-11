using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MedicalBot.Entities.Company;

namespace MedicalBot.Entities.Tasks
{
    /// <summary>
    /// Комментарий к задаче
    /// </summary>
    public class TaskComment
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Идентификатор задачи
        /// </summary>
        public Guid TaskId { get; set; }
        
        [ForeignKey("TaskId")]
        public virtual EmployeeTask Task { get; set; }

        /// <summary>
        /// Идентификатор автора комментария
        /// </summary>
        public Guid AuthorId { get; set; }
        
        [ForeignKey("AuthorId")]
        public virtual Employee Author { get; set; }

        /// <summary>
        /// Текст комментария
        /// </summary>
        [Required]
        [Column(TypeName = "text")]
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Дата создания
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}