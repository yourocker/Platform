using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CRM.Infrastructure.Security;

public sealed class TenantPermissionFilter : IAsyncAuthorizationFilter
{
    private readonly ITenantPermissionService _tenantPermissionService;
    private readonly string _permission;

    public TenantPermissionFilter(ITenantPermissionService tenantPermissionService, string permission)
    {
        _tenantPermissionService = tenantPermissionService;
        _permission = permission;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        if (!await _tenantPermissionService.HasPermissionAsync(
                context.HttpContext.User,
                _permission,
                context.HttpContext.RequestAborted))
        {
            context.Result = new ForbidResult();
        }
    }
}
