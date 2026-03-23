using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Entities.System
{
    [Table("BookingStatuses")]
    public class BookingStatus
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Name { get; set; } = string.Empty;

        public BookingStatusCategory Category { get; set; } = BookingStatusCategory.Intermediate;

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 100;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
