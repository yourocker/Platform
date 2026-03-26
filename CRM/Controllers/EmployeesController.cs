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

namespace CRM.Controllers
{
    public class EmployeesController : BasePlatformController
    {
        private readonly UserManager<Employee> _userManager;

        public EmployeesController(AppDbContext context, IWebHostEnvironment hostingEnvironment, UserManager<Employee> userManager) 
            : base(context, hostingEnvironment)
        {
            _userManager = userManager;
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

        // --- СПИСОК СОТРУДНИКОВ С ПОЛНОЙ ФИЛЬТРАЦИЕЙ И ПАГИНАЦИЕЙ ---
        public async Task<IActionResult> Index(string? searchString, int? pageNumber, int? pageSize, Dictionary<string, string> filters)
        {
            // Загружаем динамические поля для заголовков и фильтров
            await LoadDynamicFields("Employee");
            
            var query = _context.Employees
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
                        if (filter.Value == "active") query = query.Where(e => !e.IsDismissed);
                        else if (filter.Value == "dismissed") query = query.Where(e => e.IsDismissed);
                    }
                    else if (key.StartsWith("f_dyn_"))
                    {
                        // Динамические поля из конструктора
                        var fieldName = key.Replace("f_dyn_", "");
                        query = query.Where(e => EF.Functions.ILike(e.Properties, $"%\"{fieldName}\":%\"{filter.Value}\"%"));
                    }
                }
            }

            // 3. СОРТИРОВКА (Уволенные в конце)
            query = query.OrderBy(e => e.IsDismissed).ThenBy(e => e.LastName);

            // 4. ПАГИНАЦИЯ
            int actualPageSize = pageSize ?? 10;
            int actualPageNumber = pageNumber ?? 1;
            int totalItems = await query.CountAsync();
            
            var employees = await query
                .AsNoTracking()
                .Skip((actualPageNumber - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            var employeeDtos = employees.Select(EmployeeMapper.ToListDto).ToList();

            // Передача метаданных во вьюху
            ViewBag.TotalItems = totalItems;
            ViewBag.PageNumber = actualPageNumber;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)actualPageSize);
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentFilters = filters ?? new Dictionary<string, string>();

            return View(employeeDtos);
        }

        public async Task<IActionResult> Create(bool modal = false)
        {
            await LoadEmployeeViewData();
            ViewBag.IsModal = modal;
            var dto = new EmployeeCreateDto();
            EnsureAppointmentRow(dto);
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeCreateDto dto, Guid[] selectedPositions, Guid[] selectedDepartments, bool modal = false)
        {
            dto.Appointments = BuildAppointments(selectedPositions, selectedDepartments);
            dto.Phones = NormalizeContacts(dto.Phones);
            dto.Emails = NormalizeContacts(dto.Emails);
            dto.Login = string.IsNullOrWhiteSpace(dto.Login) ? null : dto.Login.Trim();

            var employee = EmployeeMapper.CreateEntity(dto);
            employee.Phones = dto.Phones;
            employee.Emails = dto.Emails;
            await SaveDynamicProperties(employee, Request.Form, "Employee");

            if (ModelState.IsValid)
            {
                employee.Id = Guid.NewGuid();
                if (!string.IsNullOrEmpty(dto.Login) && !string.IsNullOrEmpty(dto.Password))
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
                else
                {
                    _context.Add(employee);
                    await _context.SaveChangesAsync();
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
            return View(dto);
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var employee = await _context.Employees
                .Include(e => e.StaffAppointments)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();
            await LoadEmployeeViewData();
            var dto = EmployeeMapper.ToEditDto(employee);
            EnsureAppointmentRow(dto);
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, EmployeeEditDto dto, Guid[] selectedPositions, Guid[] selectedDepartments, bool modal = false)
        {
            if (id != dto.Id) return NotFound();

            dto.Appointments = BuildAppointments(selectedPositions, selectedDepartments);
            dto.Phones = NormalizeContacts(dto.Phones);
            dto.Emails = NormalizeContacts(dto.Emails);
            dto.Login = string.IsNullOrWhiteSpace(dto.Login) ? null : dto.Login.Trim();

            var dbEmployee = await _context.Employees.FindAsync(id);
            if (dbEmployee == null) return NotFound();

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
            dto.IsDismissed = dbEmployee.IsDismissed;
            EnsureAppointmentRow(dto);
            await LoadEmployeeViewData();
            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminResetPassword(Guid id, string newPassword)
        {
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
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null) { employee.IsDismissed = true; await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Edit), new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null) { employee.IsDismissed = false; await _context.SaveChangesAsync(); }
            return RedirectToAction(nameof(Edit), new { id = id });
        }
    }
}
