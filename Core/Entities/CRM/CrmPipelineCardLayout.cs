using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.MultiTenancy;

namespace Core.Entities.CRM;

public class CrmPipelineCardLayout : ITenantEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    [Required]
    public Guid PipelineId { get; set; }

    public CrmPipeline Pipeline { get; set; } = null!;

    [Column(TypeName = "jsonb")]
    public string Layout { get; set; } = "{\"sections\":[]}";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
