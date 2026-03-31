using Core.Entities.Company;
using System.Security.Claims;

namespace CRM.Infrastructure.Security;

public interface ITenantPermissionService
{
    bool HasPermission(ClaimsPrincipal user, string permission);
    Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permission, CancellationToken cancellationToken = default);
    Task<EmployeeTenantMembership?> GetCurrentMembershipAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}
