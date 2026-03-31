using CRM.ViewModels.CompanySettings;
using System.Security.Claims;

namespace CRM.Infrastructure.Security;

public interface ITenantMembershipAdministrationService
{
    Task<TenantMembersViewModel> GetPageModelAsync(
        TenantMembersFilterInput filter,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string Message)> InviteAsync(
        ClaimsPrincipal actor,
        InviteTenantMemberInput input,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string Message)> UpdateRoleAsync(
        ClaimsPrincipal actor,
        Guid membershipId,
        string roleCode,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string Message)> SetActiveAsync(
        ClaimsPrincipal actor,
        Guid membershipId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string Message)> SetDefaultAsync(
        ClaimsPrincipal actor,
        Guid membershipId,
        CancellationToken cancellationToken = default);
}
