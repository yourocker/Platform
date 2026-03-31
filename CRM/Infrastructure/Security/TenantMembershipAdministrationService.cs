using Core.Data;
using Core.Entities.Company;
using Core.MultiTenancy;
using CRM.ViewModels.CompanySettings;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CRM.Infrastructure.Security;

public sealed class TenantMembershipAdministrationService : ITenantMembershipAdministrationService
{
    private readonly AppDbContext _context;

    public TenantMembershipAdministrationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TenantMembersViewModel> GetPageModelAsync(
        TenantMembersFilterInput filter,
        ClaimsPrincipal actor,
        CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        var normalizedFilter = NormalizeFilter(filter);
        var actorId = TryGetEmployeeId(actor);

        var query = _context.EmployeeTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Employee)
            .Where(x => x.TenantId == tenant.Id);

        if (normalizedFilter.Status == "active")
        {
            query = query.Where(x => x.IsActive && !x.IsDismissed);
        }
        else if (normalizedFilter.Status == "inactive")
        {
            query = query.Where(x => !x.IsActive || x.IsDismissed);
        }

        if (!string.IsNullOrWhiteSpace(normalizedFilter.Search))
        {
            var search = normalizedFilter.Search.Trim();
            var emailSearch = search.ToUpperInvariant();

            query = query.Where(x =>
                EF.Functions.ILike(x.Employee.LastName, $"%{search}%") ||
                EF.Functions.ILike(x.Employee.FirstName, $"%{search}%") ||
                (x.Employee.MiddleName != null && EF.Functions.ILike(x.Employee.MiddleName, $"%{search}%")) ||
                (x.Employee.UserName != null && EF.Functions.ILike(x.Employee.UserName, $"%{search}%")) ||
                (x.Employee.Email != null && x.Employee.NormalizedEmail == emailSearch));
        }

        var memberships = await query
            .OrderByDescending(x => x.IsActive && !x.IsDismissed)
            .ThenBy(x => x.IsDismissed)
            .ThenByDescending(x => x.IsActive)
            .ThenByDescending(x => x.IsDefault)
            .ThenBy(x => x.Employee.LastName)
            .ThenBy(x => x.Employee.FirstName)
            .ToListAsync(cancellationToken);

        var items = memberships
            .Select(x => new TenantMemberRowViewModel
            {
                MembershipId = x.Id,
                EmployeeId = x.EmployeeId,
                FullName = string.Join(" ", new[] { x.Employee.LastName, x.Employee.FirstName, x.Employee.MiddleName }
                    .Where(value => !string.IsNullOrWhiteSpace(value))),
                Login = x.Employee.UserName,
                PrimaryEmail = x.Employee.Email,
                IsActive = x.IsActive,
                IsDismissed = x.IsDismissed,
                IsDefault = x.IsDefault,
                IsCurrentUser = actorId.HasValue && x.EmployeeId == actorId.Value,
                JoinedAt = x.JoinedAt
            })
            .ToList();

        return new TenantMembersViewModel
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            Filter = normalizedFilter,
            Invite = new InviteTenantMemberInput(),
            Items = items
        };
    }

    public async Task<(bool Success, string Message)> InviteAsync(
        ClaimsPrincipal actor,
        InviteTenantMemberInput input,
        CancellationToken cancellationToken = default)
    {
        var tenant = await GetCurrentTenantAsync(cancellationToken);
        var login = (input.Login ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(login))
        {
            return (false, "Укажите логин или email существующего аккаунта.");
        }

        var normalizedLogin = login.ToUpperInvariant();
        var employee = await _context.Employees
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                    x.UserName == login ||
                    x.NormalizedUserName == normalizedLogin ||
                    x.Email == login ||
                    x.NormalizedEmail == normalizedLogin,
                cancellationToken);

        if (employee == null)
        {
            return (false, "Аккаунт не найден. Для нового пользователя сначала создайте сотрудника или заведите ему доступ.");
        }

        var membership = await _context.EmployeeTenantMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.EmployeeId == employee.Id && x.TenantId == tenant.Id, cancellationToken);

        if (membership == null)
        {
            membership = new EmployeeTenantMembership
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                TenantId = tenant.Id,
                    RoleCode = TenantRoleCatalog.Admin,
                IsActive = true,
                IsDismissed = false,
                IsDefault = false,
                JoinedAt = DateTime.UtcNow
            };

            _context.EmployeeTenantMemberships.Add(membership);
        }
        else
        {
            membership.RoleCode = TenantRoleCatalog.Admin;
            membership.IsActive = true;
            membership.IsDismissed = false;
            membership.DismissedAt = null;
        }

        if (input.MakeDefault)
        {
            await MakeDefaultMembershipAsync(membership, cancellationToken);
        }

        if (!input.MakeDefault && !await HasAnyDefaultMembershipAsync(employee.Id, cancellationToken))
        {
            membership.IsDefault = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (true, "Участник добавлен в текущий tenant.");
    }

    public async Task<(bool Success, string Message)> UpdateRoleAsync(
        ClaimsPrincipal actor,
        Guid membershipId,
        string roleCode,
        CancellationToken cancellationToken = default)
    {
        var membership = await LoadMembershipForCurrentTenantAsync(membershipId, cancellationToken);
        if (membership == null)
        {
            return (false, "Участник tenant не найден.");
        }

        var normalizedRole = TenantRoleCatalog.Admin;
        var actorId = TryGetEmployeeId(actor);
        if (actorId.HasValue && membership.EmployeeId == actorId.Value && !string.Equals(membership.RoleCode, normalizedRole, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Нельзя менять собственную роль в активном tenant через эту страницу.");
        }

        if (membership.IsActive &&
            string.Equals(membership.RoleCode, normalizedRole, StringComparison.OrdinalIgnoreCase))
        {
            return (true, "Роль уже сохранена.");
        }

        membership.RoleCode = normalizedRole;
        await _context.SaveChangesAsync(cancellationToken);

        return (true, "Роль участника обновлена.");
    }

    public async Task<(bool Success, string Message)> SetActiveAsync(
        ClaimsPrincipal actor,
        Guid membershipId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var membership = await LoadMembershipForCurrentTenantAsync(membershipId, cancellationToken);
        if (membership == null)
        {
            return (false, "Участник tenant не найден.");
        }

        var actorId = TryGetEmployeeId(actor);
        if (actorId.HasValue && membership.EmployeeId == actorId.Value && !isActive)
        {
            return (false, "Нельзя отключить собственный доступ к активному tenant через эту страницу.");
        }

        if (membership.IsActive == isActive)
        {
            return (true, isActive ? "Доступ уже включен." : "Доступ уже отключен.");
        }

        if (isActive && membership.IsDismissed)
        {
            return (false, "Нельзя включить доступ уволенному участнику. Сначала восстановите сотрудника в текущем tenant.");
        }

        if (!isActive &&
            await IsLastPrivilegedMembershipAsync(membership.Id, cancellationToken))
        {
            return (false, "Нельзя отключить последнего пользователя с полным доступом в tenant.");
        }

        membership.IsActive = isActive;
        if (!isActive && membership.IsDefault)
        {
            membership.IsDefault = false;
            await AssignFallbackDefaultMembershipAsync(membership.EmployeeId, membership.Id, cancellationToken);
        }

        if (isActive && !await HasAnyDefaultMembershipAsync(membership.EmployeeId, cancellationToken))
        {
            membership.IsDefault = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (true, isActive ? "Доступ к tenant включен." : "Доступ к tenant отключен.");
    }

    public async Task<(bool Success, string Message)> SetDefaultAsync(
        ClaimsPrincipal actor,
        Guid membershipId,
        CancellationToken cancellationToken = default)
    {
        var membership = await LoadMembershipForCurrentTenantAsync(membershipId, cancellationToken);
        if (membership == null)
        {
            return (false, "Участник tenant не найден.");
        }

        if (!membership.IsActive || membership.IsDismissed)
        {
            return (false, "Нельзя сделать tenant по умолчанию для неактивного или уволенного участия.");
        }

        await MakeDefaultMembershipAsync(membership, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var actorId = TryGetEmployeeId(actor);
        var message = actorId.HasValue && membership.EmployeeId == actorId.Value
            ? "Текущий tenant установлен как tenant по умолчанию."
            : "Tenant по умолчанию для участника обновлен.";

        return (true, message);
    }

    private async Task<Core.Entities.System.Tenant> GetCurrentTenantAsync(CancellationToken cancellationToken)
    {
        var tenantId = _context.CurrentTenantId;
        if (!tenantId.HasValue)
        {
            throw new InvalidOperationException("Текущий tenant не определен.");
        }

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId.Value, cancellationToken);

        return tenant ?? throw new InvalidOperationException("Текущий tenant не найден.");
    }

    private async Task<EmployeeTenantMembership?> LoadMembershipForCurrentTenantAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        var tenantId = _context.CurrentTenantId;
        if (!tenantId.HasValue)
        {
            return null;
        }

        return await _context.EmployeeTenantMemberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == membershipId && x.TenantId == tenantId.Value, cancellationToken);
    }

    private async Task<bool> IsLastPrivilegedMembershipAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        var membership = await _context.EmployeeTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == membershipId, cancellationToken);

        if (membership == null)
        {
            return false;
        }

        var privilegedCount = await _context.EmployeeTenantMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == membership.TenantId && x.IsActive && !x.IsDismissed)
            .ToListAsync(cancellationToken);

        return privilegedCount.Count > 1 ? false : privilegedCount.Count == 1;
    }

    private async Task MakeDefaultMembershipAsync(EmployeeTenantMembership membership, CancellationToken cancellationToken)
    {
        var memberships = await _context.EmployeeTenantMemberships
            .IgnoreQueryFilters()
            .Where(x => x.EmployeeId == membership.EmployeeId)
            .ToListAsync(cancellationToken);

        foreach (var item in memberships)
        {
            item.IsDefault = item.Id == membership.Id;
        }
    }

    private async Task AssignFallbackDefaultMembershipAsync(Guid employeeId, Guid excludedMembershipId, CancellationToken cancellationToken)
    {
        var fallback = await _context.EmployeeTenantMemberships
            .IgnoreQueryFilters()
            .Where(x => x.EmployeeId == employeeId && x.Id != excludedMembershipId && x.IsActive && !x.IsDismissed)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.JoinedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (fallback != null)
        {
            await MakeDefaultMembershipAsync(fallback, cancellationToken);
        }
    }

    private Task<bool> HasAnyDefaultMembershipAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        return _context.EmployeeTenantMemberships
            .IgnoreQueryFilters()
            .AnyAsync(x => x.EmployeeId == employeeId && x.IsActive && !x.IsDismissed && x.IsDefault, cancellationToken);
    }

    private static Guid? TryGetEmployeeId(ClaimsPrincipal actor)
    {
        var employeeIdRaw = actor.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(employeeIdRaw, out var employeeId) ? employeeId : null;
    }

    private static TenantMembersFilterInput NormalizeFilter(TenantMembersFilterInput filter)
    {
        var normalizedStatus = (filter.Status ?? "active").Trim().ToLowerInvariant();
        if (normalizedStatus is not ("active" or "inactive" or "all"))
        {
            normalizedStatus = "active";
        }

        return new TenantMembersFilterInput
        {
            Search = filter.Search?.Trim(),
            Status = normalizedStatus
        };
    }
}
