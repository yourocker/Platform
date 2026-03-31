using System.ComponentModel.DataAnnotations;
using Core.MultiTenancy;

namespace Core.Entities.System
{
    public class StoredFile : ITenantEntity
    {
        [Key]
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        [Required]
        [MaxLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(2048)]
        public string RelativePath { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? ContentType { get; set; }

        [MaxLength(128)]
        public string Category { get; set; } = string.Empty;

        [MaxLength(128)]
        public string? OwnerEntityCode { get; set; }

        public Guid? OwnerEntityId { get; set; }

        public Guid? UploadedByEmployeeId { get; set; }

        public long Size { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
