using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Data;
using Core.DTOs.Company;
using Core.Entities.Company;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Core.Services.Company;
using Core.Entities.Platform;
using CRM.Infrastructure;
using CRM.Infrastructure.Security;
using CRM.ViewModels.Filters;
using Core.MultiTenancy;

namespace CRM.Controllers
{
    [TenantAuthorize(TenantPermissions.ManageEmployees)]
    public class EmployeesController : BasePlatformController
    {
        private readonly UserManager<Employee> _userManager;
        private readonly ITenantPermissionService _tenantPermissionService;

        public EmployeesController(
            AppDbContext context,
            IWebHostEnvironment hostingEnvironment,
            UserManager<Employee> userManager,
            ITenantPermissionService tenantPermissionService) 
            : base(context, hostingEnvironment)
        {
            _userManager = userManager;
            _tenantPermissionService = tenantPermissionService;
        }

        private async Task LoadEmployeeViewData()
        {
            await LoadDynamicFields("Employee");
            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
        }

        private static void EnsureAppointmentRow(EmployeeInputDto dto)
        {
            dto.Appointments ??= new List<EmployeeAppointmentDto>();
            if (!dto.Appointments.Any())
            {
                dto.Appointments.Add(new EmployeeAppointmentDto());
            }
        }

        private static List<EmployeeAppointmentDto> BuildAppointments(Guid[]? selectedPositions, Guid[]? selectedDepartments)
        {
            var positions = selectedPositions ?? Array.Empty<Guid>();
            var departments = selectedDepartments ?? Array.Empty<Guid>();
            var count = Math.Max(positions.Length, departments.Length);
            var appointments = new List<EmployeeAppointmentDto>();

            for (var i = 0; i < count; i++)
            {
                var positionId = i < positions.Length ? positions[i] : Guid.Empty;
                var departmentId = i < departments.Length ? departments[i] : Guid.Empty;

                if (positionId == Guid.Empty && departmentId == Guid.Empty)
                {
                    continue;
                }

                appointments.Add(new EmployeeAppointmentDto
                {
                    PositionId = positionId == Guid.Empty ? null : positionId,
                    DepartmentId = departmentId == Guid.Empty ? null : departmentId
                });
            }

            return appointments;
        }

        private static List<string> NormalizeContacts(IEnumerable<string>? values)
        {
            return values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToList() ?? new List<string>();
        }

        private async Task<Employee?> FindExistingIdentityEmployeeAsync(string? login)
        {
            if (string.IsNullOrWhiteSpace(login))
            {
                return null;
            }

            var normalizedLogin = login.Trim().ToUpperInvariant();
            return await _context.Employees
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.UserName == login.Trim() ||
                    x.NormalizedUserName == normalizedLogin ||
                    x.Email == login.Trim() ||
                    x.NormalizedEmail == normalizedLogin);
        }

        private bool CanManageEmployeeAccess() =>
            _tenantPermissionService.HasPermission(User, TenantPermissions.ManageEmployeeAccess);

        private async Task<EmployeeTenantMembership?> FindCurrentTenantMembershipAsync(Guid employeeId)
        {
            if (!_context.CurrentTenantId.HasValue)
            {
                return null;
            }

            return await _context.EmployeeTenantMemberships
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.EmployeeId == employeeId &&
                    x.TenantId == _context.CurrentTenantId.Value);
        }

        private async Task<bool> IsLastPrivilegedMembershipAsync(EmployeeTenantMembership membership)
        {
            if (membership.IsDismissed ||
                !membership.IsActive)
            {
                return false;
            }

            var privilegedCount = await _context.EmployeeTenantMemberships
                .IgnoreQueryFilters()
                .CountAsync(x =>
                    x.TenantId == membership.TenantId &&
                    x.IsActive &&
                    !x.IsDismissed);

            return privilegedCount <= 1;
        }

        private Dictionary<string, object> ExtractDynamicProps()
        {
            var dict = new Dictionary<string, object>();

            foreach (var key in Request.Form.Keys.Where(k => k.StartsWith("DynamicProps[")))
            {
                var systemName = key.Replace("DynamicProps[", "").Replace("]", "");
                var values = Request.Form[key].ToList();
                dict[systemName] = values.Count > 1 ? values : (values.FirstOrDefault() ?? "");
            }

            return dict;
        }

        private FilterPanelViewModel BuildFilterPanelModel(
            IReadOnlyCollection<AppFieldDefinition> dynamicFields,
            IDictionary<string, string> currentFilters,
            int pageSize)
        {
            var lookupData = ViewBag.LookupData as Dictionary<string, List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>>
                             ?? new Dictionary<string, List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>>();

            var fields = new List<FilterFieldViewModel>
            {
                new() { Key = "f_LastName", Label = "Фамилия", Kind = FilterInputKind.Text, Value = TryGetFilterValue(currentFilters, "f_LastName") },
                new() { Key = "f_FirstName", Label = "Имя", Kind = FilterInputKind.Text, Value = TryGetFilterValue(currentFilters, "f_FirstName") },
                new() { Key = "f_MiddleName", Label = "Отчество", Kind = FilterInputKind.Text, Value = TryGetFilterValue(currentFilters, "f_MiddleName") },
                new() { Key = "f_Phone", Label = "Телефон", Kind = FilterInputKind.Text, Value = TryGetFilterValue(currentFilters, "f_Phone") },
                new()
                {
                    Key = "f_Status",
                    Label = "Статус сотрудника",
                    Kind = FilterInputKind.Select,
                    Value = TryGetFilterValue(currentFilters, "f_Status"),
                    Options = new List<FilterOptionViewModel>
                    {
                        new() { Value = "active", Label = "Работает" },
                        new() { Value = "dismissed", Label = "Уволен" }
                    }
                }
            };

            fields.AddRange(BuildDynamicFilterFields(dynamicFields, lookupData, currentFilters));

            return new FilterPanelViewModel
            {
                ActionUrl = Url.Action(nameof(Index)) ?? "/Employees",
                ResetUrl = Url.Action(nameof(Index)) ?? "/Employees",
                EntityCode = "Employee",
                ViewCode = "Index",
                SearchValue = ViewBag.CurrentSearch as string ?? string.Empty,
                SearchPlaceholder = "Быстрый поиск",
                PageSize = pageSize,
                ExpandedByDefault = currentFilters.Any(),
                Fields = fields
            };
        }

        // --- СПИСОК СОТРУДНИКОВ С ПОЛНОЙ ФИЛЬТРАЦИЕЙ И ПАГИНАЦИЕЙ ---
        public async Task<IActionResult> Index(string? searchString, int? pageNumber, int? pageSize, Dictionary<string, string> filters)
        {
            if (!_context.CurrentTenantId.HasValue)
            {
                return Forbid();
            }

            var currentTenantId = _context.CurrentTenantId.Value;

            // Загружаем динамические поля для заголовков и фильтров
            await LoadDynamicFields("Employee");
            var dynamicFields = ViewBag.DynamicFields as List<AppFieldDefinition> ?? new List<AppFieldDefinition>();
            var dynamicFieldMap = dynamicFields.ToDictionary(field => field.SystemName, field => field, StringComparer.OrdinalIgnoreCase);
            
            var query = _context.Employees
                .Include(e => e.TenantMemberships.Where(m => m.TenantId == currentTenantId))
                .Include(e => e.StaffAppointments).ThenInclude(a => a.Position)
                .Include(e => e.StaffAppointments).ThenInclude(a => a.Department)
                .AsQueryable();

            // 1. БЫСТРЫЙ ПОИСК (LastName, FirstName, UserName, Любой из телефонов)
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(e => 
                    EF.Functions.ILike(e.LastName, $"%{searchString}%") || 
                    EF.Functions.ILike(e.FirstName, $"%{searchString}%") ||
                    EF.Functions.ILike(e.UserName, $"%{searchString}%") ||
                    e.Phones.Any(p => EF.Functions.ILike(p, $"%{searchString}%")));
            }

            // 2. ПОЛНАЯ ФИЛЬТРАЦИЯ (Обработка словаря параметров)
            if (filters != null && filters.Any())
            {
                foreach (var filter in filters)
                {
                    if (string.IsNullOrWhiteSpace(filter.Value)) continue;

                    // Ключи приходят как f_LastName, f_dyn_SystemName и т.д.
                    var key = filter.Key;

                    if (key == "f_LastName")
                        query = query.Where(e => EF.Functions.ILike(e.LastName, $"%{filter.Value}%"));
                    else if (key == "f_FirstName")
                        query = query.Where(e => EF.Functions.ILike(e.FirstName, $"%{filter.Value}%"));
                    else if (key == "f_MiddleName")
                        query = query.Where(e => EF.Functions.ILike(e.MiddleName, $"%{filter.Value}%"));
                    else if (key == "f_Phone")
                        query = query.Where(e => e.Phones.Any(p => EF.Functions.ILike(p, $"%{filter.Value}%")));
                    else if (key == "f_Status")
                    {
                        if (filter.Value == "active")
                        {
                            query = query.Where(e => e.TenantMemberships.Any(m =>
                                m.TenantId == currentTenantId &&
                                m.IsActive &&
                                !m.IsDismissed));
                        }
                        else if (filter.Value == "dismissed")
                        {
                            query = query.Where(e => e.TenantMemberships.Any(m =>
                                m.TenantId == currentTenantId &&
                                m.IsActive &&
                                m.IsDismissed));
                        }
                    }
                    else if (key.StartsWith("f_dyn_"))
                    {
                        var fieldName = key.Replace("f_dyn_", "");
                        if (dynamicFieldMap.TryGetValue(fieldName, out var field))
                        {
                            query = query.ApplyDynamicPropertyFilter(nameof(Employee.Properties), field, filter.Value);
                        }
                    }
                }
            }

            // 3. СОРТИРОВКА (Уволенные в конце)
            query = query
                .OrderBy(e => e.TenantMemberships
                    .Where(m => m.TenantId == currentTenantId && m.IsActive)
                    .Select(m => m.IsDismissed)
                    .FirstOrDefault())
                .ThenBy(e => e.LastName);

            // 4. ПАГИНАЦИЯ
            int actualPageSize = pageSize ?? 10;
            int actualPageNumber = pageNumber ?? 1;
            int totalItems = await query.CountAsync();
            
            var employees = await query
                .AsNoTracking()
                .Skip((actualPageNumber - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            var employeeDtos = employees
                .Select(employee =>
                {
                    var membership = employee.TenantMemberships.FirstOrDefault(x => x.TenantId == currentTenantId);
                    return EmployeeMapper.ToListDto(employee, membership?.IsDismissed == true);
                })
                .ToList();

            // Передача метаданных во вьюху
            ViewBag.TotalItems = totalItems;
            ViewBag.PageNumber = actualPageNumber;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)actualPageSize);
            var currentFilters = filters ?? new Dictionary<string, string>();
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentFilters = currentFilters;
            ViewBag.FilterPanelModel = BuildFilterPanelModel(dynamicFields, currentFilters, actualPageSize);

            return View(employeeDtos);
        }

        public async Task<IActionResult> Create(bool modal = false)
        {
            await LoadEmployeeViewData();
            ViewBag.IsModal = modal;
            ViewBag.CanManageEmployeeAccess = CanManageEmployeeAccess();
            var dto = new EmployeeCreateDto();
            EnsureAppointmentRow(dto);
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeCreateDto dto, Guid[] selectedPositions, Guid[] selectedDepartments, bool modal = false)
        {
            if (!_context.CurrentTenantId.HasValue)
            {
                return Forbid();
            }

            var canManageEmployeeAccess = CanManageEmployeeAccess();
            dto.Appointments = BuildAppointments(selectedPositions, selectedDepartments);
            dto.Phones = NormalizeContacts(dto.Phones);
            dto.Emails = NormalizeContacts(dto.Emails);
            dto.Login = string.IsNullOrWhiteSpace(dto.Login) ? null : dto.Login.Trim();

            if (!canManageEmployeeAccess && (!string.IsNullOrWhiteSpace(dto.Login) || !string.IsNullOrWhiteSpace(dto.Password)))
            {
                ModelState.AddModelError(string.Empty, "Выдавать доступ в систему может только администратор tenant или техподдержка.");
            }

            var employee = EmployeeMapper.CreateEntity(dto);
            employee.Id = Guid.NewGuid();
            employee.TenantId = _context.CurrentTenantId ?? employee.TenantId;
            employee.Phones = dto.Phones;
            employee.Emails = dto.Emails;
            await SaveDynamicProperties(employee, Request.Form, "Employee");
            var candidateProperties = employee.Properties;

            if (ModelState.IsValid)
            {
                var existingIdentityEmployee = canManageEmployeeAccess
                    ? await FindExistingIdentityEmployeeAsync(dto.Login)
                    : null;

                if (existingIdentityEmployee != null && _context.CurrentTenantId.HasValue)
                {
                    var currentTenantId = _context.CurrentTenantId.Value;
                    var alreadyInTenant = await _context.EmployeeTenantMemberships
                        .IgnoreQueryFilters()
                        .AnyAsync(x => x.EmployeeId == existingIdentityEmployee.Id && x.TenantId == currentTenantId);

                    if (alreadyInTenant)
                    {
                        ModelState.AddModelError(nameof(dto.Login), "Пользователь с таким логином уже добавлен в текущий tenant.");
                    }
                    else
                    {
                        employee = existingIdentityEmployee;
                        employee.LastName = dto.LastName.Trim();
                        employee.FirstName = dto.FirstName.Trim();
                        employee.MiddleName = string.IsNullOrWhiteSpace(dto.MiddleName) ? null : dto.MiddleName.Trim();
                        employee.Phones = dto.Phones;
                        employee.Emails = dto.Emails;
                        employee.Properties = candidateProperties;

                        var updateResult = await _userManager.UpdateAsync(employee);
                        if (!updateResult.Succeeded)
                        {
                            foreach (var error in updateResult.Errors) ModelState.AddModelError("", error.Description);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(dto.Password) && !await _userManager.HasPasswordAsync(employee))
                            {
                                var passwordResult = await _userManager.AddPasswordAsync(employee, dto.Password);
                                if (!passwordResult.Succeeded)
                                {
                                    foreach (var error in passwordResult.Errors) ModelState.AddModelError("", error.Description);
                                }
                            }

                            if (ModelState.IsValid)
                            {
                                var hasAnyMemberships = await _context.EmployeeTenantMemberships
                                    .IgnoreQueryFilters()
                                    .AnyAsync(x => x.EmployeeId == employee.Id);

                                await DbInitializer.EnsureEmployeeMembershipAsync(
                                    _context,
                                    employee.Id,
                                    currentTenantId,
                                    "employee",
                                    isDefault: !hasAnyMemberships);
                            }
                        }
                    }
                }

                if (ModelState.IsValid && existingIdentityEmployee == null && canManageEmployeeAccess && !string.IsNullOrEmpty(dto.Login) && !string.IsNullOrEmpty(dto.Password))
                {
                    employee.UserName = dto.Login;
                    employee.Email = dto.Login.Contains("@") ? dto.Login : null;
                    var result = await _userManager.CreateAsync(employee, dto.Password);
                    if (!result.Succeeded)
                    {
                        foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
                        dto.DynamicValues = ExtractDynamicProps();
                        dto.Password = null;
                        EnsureAppointmentRow(dto);
                        await LoadEmployeeViewData();
                        ViewBag.IsModal = modal;
                        return View(dto);
                    }
                }
                else if (ModelState.IsValid && existingIdentityEmployee == null)
                {
                    _context.Add(employee);
                    await _context.SaveChangesAsync();
                }

                if (ModelState.IsValid && _context.CurrentTenantId.HasValue && existingIdentityEmployee == null)
                {
                    await DbInitializer.EnsureEmployeeMembershipAsync(
                        _context,
                        employee.Id,
                        _context.CurrentTenantId.Value,
                        "employee",
                        isDefault: true);
                }

                var appointments = EmployeeMapper.ToStaffAppointments(employee.Id, dto.Appointments);
                if (appointments.Any())
                {
                    _context.StaffAppointments.AddRange(appointments);
                }

                await _context.SaveChangesAsync();
                FinalizeDynamicFilePaths(employee, "Employee", employee.Id.ToString());
                await _context.SaveChangesAsync();

                if (modal)
                {
                    return BuildModalCreatedContentResult("Employee", employee.Id, employee.FullName);
                }

                return RedirectToAction(nameof(Index));
            }
            dto.DynamicValues = ExtractDynamicProps();
            dto.Password = null;
            EnsureAppointmentRow(dto);
            await LoadEmployeeViewData();
            ViewBag.IsModal = modal;
            ViewBag.CanManageEmployeeAccess = canManageEmployeeAccess;
            return View(dto);
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            if (!_context.CurrentTenantId.HasValue) return Forbid();

            var currentTenantId = _context.CurrentTenantId.Value;
            var employee = await _context.Employees
                .Include(e => e.TenantMemberships.Where(m => m.TenantId == currentTenantId))
                .Include(e => e.StaffAppointments)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();
            var membership = employee.TenantMemberships.FirstOrDefault(m => m.TenantId == currentTenantId);
            if (membership == null) return NotFound();
            await LoadEmployeeViewData();
            ViewBag.CanManageEmployeeAccess = CanManageEmployeeAccess();
            var dto = EmployeeMapper.ToEditDto(employee, membership.IsDismissed);
            EnsureAppointmentRow(dto);
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, EmployeeEditDto dto, Guid[] selectedPositions, Guid[] selectedDepartments, bool modal = false)
        {
            if (id != dto.Id) return NotFound();
            if (!_context.CurrentTenantId.HasValue) return Forbid();

            var canManageEmployeeAccess = CanManageEmployeeAccess();

            dto.Appointments = BuildAppointments(selectedPositions, selectedDepartments);
            dto.Phones = NormalizeContacts(dto.Phones);
            dto.Emails = NormalizeContacts(dto.Emails);
            dto.Login = string.IsNullOrWhiteSpace(dto.Login) ? null : dto.Login.Trim();

            var dbEmployee = await _context.Employees
                .Include(e => e.TenantMemberships.Where(m => m.TenantId == _context.CurrentTenantId.Value))
                .FirstOrDefaultAsync(e => e.Id == id);
            if (dbEmployee == null) return NotFound();
            var currentMembership = dbEmployee.TenantMemberships.FirstOrDefault(m => m.TenantId == _context.CurrentTenantId.Value);
            if (currentMembership == null) return NotFound();

            if (!canManageEmployeeAccess)
            {
                var attemptedLoginChange = Request.Form.ContainsKey(nameof(dto.Login)) &&
                                           !string.Equals(dto.Login, dbEmployee.UserName, StringComparison.OrdinalIgnoreCase);
                if (attemptedLoginChange || !string.IsNullOrWhiteSpace(dto.NewPassword))
                {
                    ModelState.AddModelError(string.Empty, "Изменять доступ в систему может только администратор tenant или техподдержка.");
                }

                dto.Login = dbEmployee.UserName;
                dto.NewPassword = null;
            }

            await SaveDynamicProperties(dbEmployee, Request.Form, "Employee");

            if (ModelState.IsValid)
            {
                try
                {
                    FinalizeDynamicFilePaths(dbEmployee, "Employee", dbEmployee.Id.ToString());
                    EmployeeMapper.UpdateEntity(dbEmployee, dto);
                    dbEmployee.Phones = dto.Phones;
                    dbEmployee.Emails = dto.Emails;

                    if (!string.IsNullOrEmpty(dto.Login))
                    {
                        if (string.IsNullOrEmpty(dbEmployee.UserName) && !string.IsNullOrEmpty(dto.NewPassword))
                        {
                            dbEmployee.UserName = dto.Login;
                            dbEmployee.Email = dto.Login.Contains("@") ? dto.Login : null;
                            await _userManager.UpdateNormalizedUserNameAsync(dbEmployee);
                            await _userManager.UpdateNormalizedEmailAsync(dbEmployee);
                            await _userManager.AddPasswordAsync(dbEmployee, dto.NewPassword);
                        }
                        else if (dbEmployee.UserName != dto.Login)
                        {
                            await _userManager.SetUserNameAsync(dbEmployee, dto.Login);
                            if (dto.Login.Contains("@")) await _userManager.SetEmailAsync(dbEmployee, dto.Login);
                        }
                    }

                    var existingApps = _context.StaffAppointments.Where(a => a.EmployeeId == id);
                    _context.StaffAppointments.RemoveRange(existingApps);

                    var appointments = EmployeeMapper.ToStaffAppointments(id, dto.Appointments);
                    if (appointments.Any())
                    {
                        _context.StaffAppointments.AddRange(appointments);
                    }
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeExists(dto.Id)) return NotFound(); else throw;
                }
                if (modal)
                {
                    return BuildModalUpdatedContentResult("Employee", id, dbEmployee.FullName);
                }

                return RedirectToAction(nameof(Index));
            }

            dto.DynamicValues = ExtractDynamicProps();
            dto.NewPassword = null;
            dto.IsDismissed = currentMembership.IsDismissed;
            EnsureAppointmentRow(dto);
            await LoadEmployeeViewData();
            ViewBag.CanManageEmployeeAccess = canManageEmployeeAccess;
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantAuthorize(TenantPermissions.ManageEmployeeAccess)]
        public async Task<IActionResult> AdminResetPassword(Guid id, string newPassword)
        {
            var membership = await FindCurrentTenantMembershipAsync(id);
            if (membership == null)
            {
                return NotFound("Сотрудник текущего tenant не найден");
            }

            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound("Сотрудник не найден");
            if (await _userManager.HasPasswordAsync(user)) await _userManager.RemovePasswordAsync(user);
            var result = await _userManager.AddPasswordAsync(user, newPassword);
            return result.Succeeded ? Ok() : BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        private bool EmployeeExists(Guid id) => _context.Employees.Any(e => e.Id == id);
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dismiss(Guid id)
        {
            var membership = await FindCurrentTenantMembershipAsync(id);
            var currentUserIdRaw = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (membership == null)
            {
                return RedirectToAction(nameof(Index));
            }

            if (Guid.TryParse(currentUserIdRaw, out var currentUserId) && membership.EmployeeId == currentUserId)
            {
                TempData["Error"] = "Нельзя уволить самого себя из текущего tenant.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            if (await IsLastPrivilegedMembershipAsync(membership))
            {
                TempData["Error"] = "Нельзя уволить последнего пользователя с доступом в tenant.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            membership.IsDismissed = true;
            membership.DismissedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid id)
        {
            var membership = await FindCurrentTenantMembershipAsync(id);
            if (membership != null)
            {
                membership.IsDismissed = false;
                membership.DismissedAt = null;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Edit), new { id = id });
        }
    }
}
