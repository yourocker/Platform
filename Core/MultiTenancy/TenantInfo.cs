namespace Core.MultiTenancy;

public sealed class TenantInfo
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}
