using Core.Data;
using Core.Entities.Company;
using Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CRM.Infrastructure.Security;

public sealed class TenantPermissionService : ITenantPermissionService
{
    private static readonly HashSet<string> FullAccessPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        TenantPermissions.ManageTenantSettings,
        TenantPermissions.ManageTenantMembers,
        TenantPermissions.ManageConstructors,
        TenantPermissions.ManageEmployees,
        TenantPermissions.ManageEmployeeAccess,
        TenantPermissions.ManageCompanyStructure,
        TenantPermissions.ManageServiceCatalog
    };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> PermissionMatrix =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [TenantRoleCatalog.Admin] = new(FullAccessPermissions, StringComparer.OrdinalIgnoreCase)
        };

    private readonly AppDbContext _context;

    public TenantPermissionService(AppDbContext context)
    {
        _context = context;
    }

    public bool HasPermission(ClaimsPrincipal user, string permission)
    {
        var roleCode = user.FindFirstValue("tenant_role") ?? user.FindFirstValue(ClaimTypes.Role);
        return HasPermission(TenantRoleCatalog.Normalize(roleCode), permission);
    }

    public async Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var membership = await GetCurrentMembershipAsync(user, cancellationToken);
        if (membership == null)
        {
            return false;
        }

        return HasPermission(membership.RoleCode, permission);
    }

    public Task<EmployeeTenantMembership?> GetCurrentMembershipAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _context.CurrentTenantId;
        var employeeId = TryGetEmployeeId(user);
        if (!tenantId.HasValue || !employeeId.HasValue)
        {
            return Task.FromResult<EmployeeTenantMembership?>(null);
        }

        return _context.EmployeeTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x =>
                    x.EmployeeId == employeeId.Value &&
                    x.TenantId == tenantId.Value &&
                    x.IsActive &&
                    !x.IsDismissed &&
                    x.Tenant.IsActive,
                cancellationToken);
    }

    private static bool HasPermission(string roleCode, string permission)
    {
        var normalizedRole = TenantRoleCatalog.Normalize(roleCode);
        return PermissionMatrix.TryGetValue(normalizedRole, out var permissions) &&
               permissions.Contains(permission);
    }

    private static Guid? TryGetEmployeeId(ClaimsPrincipal user)
    {
        var employeeIdRaw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(employeeIdRaw, out var employeeId) ? employeeId : null;
    }
}
