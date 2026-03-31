namespace Core.MultiTenancy;

public sealed record TenantRoleDefinition(string Code, string DisplayName, string Description);

public static class TenantRoleCatalog
{
    public const string Admin = "admin";

    private static readonly IReadOnlyList<TenantRoleDefinition> Definitions =
    [
        new(Admin, "Полный доступ", "Временный режим до появления настраиваемого разграничения прав.")
    ];

    public static IReadOnlyList<TenantRoleDefinition> GetAll() => Definitions;

    public static string Normalize(string? roleCode)
    {
        return Admin;
    }

    public static string GetDisplayName(string? roleCode)
    {
        return Definitions[0].DisplayName;
    }
}
