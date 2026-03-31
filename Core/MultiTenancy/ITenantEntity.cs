namespace Core.MultiTenancy;

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}
