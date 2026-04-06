using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.MultiTenancy;

namespace Core.Entities.CRM;

[Table("CrmActivityBindings")]
public class CrmActivityBinding : ITenantEntity
{
    public Guid ActivityId { get; set; }

    [ForeignKey(nameof(ActivityId))]
    public virtual CrmActivity Activity { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string EntityCode { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public Guid TenantId { get; set; }

    public bool IsPrimary { get; set; }
}
