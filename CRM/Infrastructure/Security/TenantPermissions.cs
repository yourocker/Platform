namespace CRM.Infrastructure.Security;

public static class TenantPermissions
{
    public const string ManageTenantSettings = "tenant.settings.manage";
    public const string ManageTenantMembers = "tenant.members.manage";
    public const string ManageConstructors = "tenant.constructors.manage";
    public const string ManageEmployees = "tenant.employees.manage";
    public const string ManageEmployeeAccess = "tenant.employees.access.manage";
    public const string ManageCompanyStructure = "tenant.company-structure.manage";
    public const string ManageServiceCatalog = "tenant.service-catalog.manage";
}
