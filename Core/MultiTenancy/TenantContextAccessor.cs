namespace Core.MultiTenancy;

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    public TenantInfo? CurrentTenant { get; set; }
}
