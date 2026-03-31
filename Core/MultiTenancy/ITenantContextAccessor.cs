namespace Core.MultiTenancy;

public interface ITenantContextAccessor
{
    TenantInfo? CurrentTenant { get; set; }
}
