using System.ComponentModel.DataAnnotations;

namespace MedicalBot.Entities.Platform
{
    public class AppDefinition
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [MaxLength(50)]
        public string SystemCode { get; set; }

        public bool IsSystem { get; set; } // true для Employee, false для кастомных

        public string? Icon { get; set; } // Название иконки bootstrap-icons

        // Навигационное свойство для полей
        public virtual ICollection<AppFieldDefinition> Fields { get; set; } = new List<AppFieldDefinition>();
    }
}