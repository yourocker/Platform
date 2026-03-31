namespace Core.MultiTenancy;

public sealed class TenantResolutionOptions
{
    public const string SectionName = "Tenancy";

    public string DefaultTenantKey { get; set; } = "default";
    public string HeaderName { get; set; } = "X-Tenant";
    public string QueryParameterName { get; set; } = "tenant";
    public bool ResolveFromSubdomain { get; set; } = true;
}
