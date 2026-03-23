using System.ComponentModel.DataAnnotations;
using Core.Entities.Company;

namespace Core.Entities.System
{
    /// <summary>
    /// Сохраненный пользовательский пресет фильтров для конкретной сущности и представления.
    /// </summary>
    public class UserFilterPreset
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// Пользователь (Employee / Identity User), которому принадлежит пресет.
        /// </summary>
        public Guid UserId { get; set; }

        public virtual Employee User { get; set; } = null!;

        /// <summary>
        /// Код сущности, к которой привязан пресет (например ResourceBooking).
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string EntityCode { get; set; } = string.Empty;

        /// <summary>
        /// Код представления/экрана для расширяемости (например ScheduleCalendar).
        /// </summary>
        [Required]
        [MaxLength(64)]
        public string ViewCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Набор фильтров в виде JSON-объекта key/value.
        /// </summary>
        [Required]
        public string FiltersJson { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
