using System.ComponentModel.DataAnnotations;

namespace Core.Entities.System
{
    public class Tenant
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(1024)]
        public string? Notes { get; set; }
    }
}
