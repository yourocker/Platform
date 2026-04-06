using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Entities;
using Core.Entities.Company;
using Core.MultiTenancy;

namespace Core.Entities.CRM
{
    [Table("CrmEntityRelations")]
    public class CrmEntityRelation : ITenantEntity, ISoftDeletable
    {
        [Key]
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        [Required]
        [MaxLength(64)]
        public string SourceEntityCode { get; set; } = string.Empty;

        public Guid SourceEntityId { get; set; }

        [Required]
        [MaxLength(64)]
        public string TargetEntityCode { get; set; } = string.Empty;

        public Guid TargetEntityId { get; set; }

        [Required]
        [MaxLength(64)]
        public string RelationType { get; set; } = "linked";

        public Guid? CreatedByEmployeeId { get; set; }

        [ForeignKey(nameof(CreatedByEmployeeId))]
        public virtual Employee? CreatedByEmployee { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }
    }
}
