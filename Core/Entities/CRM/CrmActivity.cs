using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Core.Entities.Company;
using Core.Entities.Tasks;
using Core.MultiTenancy;

namespace Core.Entities.CRM;

public enum CrmActivityType
{
    Comment = 0,
    Task = 1,
    Call = 2,
    Email = 3,
    Meeting = 4,
    Message = 5,
    Note = 6,
    StatusChange = 7
}

[Table("CrmActivities")]
public class CrmActivity : ITenantEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public CrmActivityType Type { get; set; }

    [MaxLength(255)]
    public string Subject { get; set; } = string.Empty;

    [Column(TypeName = "text")]
    public string? Content { get; set; }

    public Guid? AuthorId { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public virtual Employee? Author { get; set; }

    public Guid? LinkedTaskId { get; set; }

    [ForeignKey(nameof(LinkedTaskId))]
    public virtual EmployeeTask? LinkedTask { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DueAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public bool IsPinned { get; set; }

    public virtual ICollection<CrmActivityBinding> Bindings { get; set; } = new List<CrmActivityBinding>();
}
