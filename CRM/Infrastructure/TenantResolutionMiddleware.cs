using Core.Data;
using Core.Entities.Company;
using Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace CRM.Infrastructure;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContextAccessor tenantContextAccessor,
        IOptions<TenantResolutionOptions> optionsAccessor,
        AppDbContext dbContext)
    {
        var options = optionsAccessor.Value;
        var tenant = await ResolveTenantAsync(context, options, dbContext);
        if (tenant != null)
        {
            tenantContextAccessor.CurrentTenant = tenant;
        }

        await _next(context);
    }

    private static async Task<TenantInfo?> ResolveTenantAsync(
        HttpContext context,
        TenantResolutionOptions options,
        AppDbContext dbContext)
    {
        var employeeId = TryGetEmployeeId(context);
        var requestedTenantKey = ResolveTenantKeyFromRequest(context, options);
        var claimTenantId = context.User.FindFirstValue("tenant_id");

        if (employeeId.HasValue)
        {
            if (Guid.TryParse(claimTenantId, out var tenantIdFromClaim))
            {
                var membershipByClaim = await FindActiveMembershipAsync(dbContext, employeeId.Value, tenantIdFromClaim);
                if (membershipByClaim != null)
                {
                    return ToTenantInfo(membershipByClaim.Tenant);
                }
            }

            if (!string.IsNullOrWhiteSpace(requestedTenantKey))
            {
                var membershipByKey = await FindActiveMembershipByTenantKeyAsync(dbContext, employeeId.Value, requestedTenantKey!);
                if (membershipByKey != null)
                {
                    return ToTenantInfo(membershipByKey.Tenant);
                }
            }

            var defaultMembership = await dbContext.EmployeeTenantMemberships
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(x => x.Tenant)
                .Where(x => x.EmployeeId == employeeId.Value && x.IsActive && x.Tenant.IsActive)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.JoinedAt)
                .FirstOrDefaultAsync();

            if (defaultMembership != null)
            {
                return ToTenantInfo(defaultMembership.Tenant);
            }
        }

        if (Guid.TryParse(claimTenantId, out var tenantIdFromClaimForAnonymous))
        {
            var claimedTenant = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantIdFromClaimForAnonymous && t.IsActive);

            if (claimedTenant != null)
            {
                return ToTenantInfo(claimedTenant);
            }
        }

        if (!string.IsNullOrWhiteSpace(requestedTenantKey))
        {
            var tenantByKey = await dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Key == requestedTenantKey && t.IsActive);

            if (tenantByKey != null)
            {
                return ToTenantInfo(tenantByKey);
            }
        }

        var fallbackTenant = await dbContext.Tenants
            .AsNoTracking()
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(t => t.Key == options.DefaultTenantKey && t.IsActive);

        if (fallbackTenant != null)
        {
            return ToTenantInfo(fallbackTenant);
        }

        var firstActiveTenant = await dbContext.Tenants
            .AsNoTracking()
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(t => t.IsActive);

        return firstActiveTenant == null ? null : ToTenantInfo(firstActiveTenant);
    }

    private static Guid? TryGetEmployeeId(HttpContext context)
    {
        var employeeIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(employeeIdRaw, out var employeeId) ? employeeId : null;
    }

    private static Task<EmployeeTenantMembership?> FindActiveMembershipAsync(
        AppDbContext dbContext,
        Guid employeeId,
        Guid tenantId)
    {
        return dbContext.EmployeeTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x =>
                x.EmployeeId == employeeId &&
                x.TenantId == tenantId &&
                x.IsActive &&
                x.Tenant.IsActive);
    }

    private static Task<EmployeeTenantMembership?> FindActiveMembershipByTenantKeyAsync(
        AppDbContext dbContext,
        Guid employeeId,
        string tenantKey)
    {
        return dbContext.EmployeeTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x =>
                x.EmployeeId == employeeId &&
                x.IsActive &&
                x.Tenant.IsActive &&
                x.Tenant.Key == tenantKey);
    }

    private static string? ResolveTenantKeyFromRequest(HttpContext context, TenantResolutionOptions options)
    {
        if (context.Request.Headers.TryGetValue(options.HeaderName, out var headerValues))
        {
            var headerTenant = headerValues.FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(headerTenant))
            {
                return headerTenant;
            }
        }

        if (context.Request.Query.TryGetValue(options.QueryParameterName, out var queryValues))
        {
            var queryTenant = queryValues.FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(queryTenant))
            {
                return queryTenant;
            }
        }

        if (!options.ResolveFromSubdomain)
        {
            return null;
        }

        var host = context.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        return parts[0].Trim();
    }

    private static TenantInfo ToTenantInfo(Core.Entities.System.Tenant tenant) =>
        new()
        {
            Id = tenant.Id,
            Key = tenant.Key,
            Name = tenant.Name,
            IsActive = tenant.IsActive
        };
}
