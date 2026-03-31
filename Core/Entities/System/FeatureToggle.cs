using System.ComponentModel.DataAnnotations;
using Core.MultiTenancy;

namespace Core.Entities.System
{
    /// <summary>
    /// Переключатели модулей платформы (под тарифы/лицензии).
    /// </summary>
    public class FeatureToggle : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }

        [Required]
        [MaxLength(64)]
        public string FeatureCode { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        [MaxLength(256)]
        public string? Description { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
