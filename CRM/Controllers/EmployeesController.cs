using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Data;
using Core.Entities.Company;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;

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
                .Skip((actualPageNumber - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            // Передача метаданных во вьюху
            ViewBag.TotalItems = totalItems;
            ViewBag.PageNumber = actualPageNumber;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)actualPageSize);
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentFilters = filters ?? new Dictionary<string, string>();

            return View(employees);
        }

        public async Task<IActionResult> Create()
        {
            await LoadDynamicFields("Employee"); 
            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee, Guid[] selectedPositions, Guid[] selectedDepartments, string[] Phones, string[] Emails, IFormCollection form, string? Login, string? Password)
        {
            employee.Phones = Phones?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
            employee.Emails = Emails?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();
            await SaveDynamicProperties(employee, form, "Employee");
            ModelState.Remove("Login");
            ModelState.Remove("Password");

            if (ModelState.IsValid)
            {
                employee.Id = Guid.NewGuid();
                if (!string.IsNullOrEmpty(Login) && !string.IsNullOrEmpty(Password))
                {
                    employee.UserName = Login;
                    employee.Email = Login.Contains("@") ? Login : null;
                    var result = await _userManager.CreateAsync(employee, Password);
                    if (!result.Succeeded)
                    {
                        foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
                        await LoadDynamicFields("Employee");
                        ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
                        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
                        return View(employee);
                    }
                }
                else
                {
                    _context.Add(employee);
                    await _context.SaveChangesAsync();
                }

                if (selectedPositions != null && selectedDepartments != null)
                {
                    for (int i = 0; i < selectedPositions.Length; i++)
                    {
                        if (i < selectedDepartments.Length)
                        {
                            _context.StaffAppointments.Add(new StaffAppointment
                            {
                                Id = Guid.NewGuid(),
                                EmployeeId = employee.Id,
                                PositionId = selectedPositions[i],
                                DepartmentId = selectedDepartments[i],
                                IsPrimary = (i == 0)
                            });
                        }
                    }
                }
                await _context.SaveChangesAsync();
                FinalizeDynamicFilePaths(employee, "Employee", employee.Id.ToString());
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await LoadDynamicFields("Employee");
            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View(employee);
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var employee = await _context.Employees.Include(e => e.StaffAppointments).FirstOrDefaultAsync(e => e.Id == id);
            if (employee == null) return NotFound();
            await LoadDynamicFields("Employee");
            ViewBag.Departments = await _context.Departments.ToListAsync();
            ViewBag.Positions = await _context.Positions.ToListAsync();
            ViewBag.HasLogin = !string.IsNullOrEmpty(employee.UserName);
            return View(employee);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Employee employee, Guid[] selectedPositions, Guid[] selectedDepartments, string[] Phones, string[] Emails, IFormCollection form, string? Login, string? NewPassword)
        {
            if (id != employee.Id) return NotFound();
            await SaveDynamicProperties(employee, form, "Employee");
            ModelState.Remove("Login");
            ModelState.Remove("NewPassword");

            if (ModelState.IsValid)
            {
                try
                {
                    FinalizeDynamicFilePaths(employee, "Employee", employee.Id.ToString());
                    var dbEmployee = await _context.Employees.FindAsync(id);
                    if (dbEmployee == null) return NotFound();

                    dbEmployee.FirstName = employee.FirstName;
                    dbEmployee.LastName = employee.LastName;
                    dbEmployee.MiddleName = employee.MiddleName;
                    dbEmployee.IsDismissed = employee.IsDismissed;
                    dbEmployee.Properties = employee.Properties;
                    dbEmployee.Phones = Phones?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
                    dbEmployee.Emails = Emails?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();

                    if (!string.IsNullOrEmpty(Login))
                    {
                        if (string.IsNullOrEmpty(dbEmployee.UserName) && !string.IsNullOrEmpty(NewPassword))
                        {
                            dbEmployee.UserName = Login;
                            dbEmployee.Email = Login.Contains("@") ? Login : null;
                            await _userManager.UpdateNormalizedUserNameAsync(dbEmployee);
                            await _userManager.UpdateNormalizedEmailAsync(dbEmployee);
                            await _userManager.AddPasswordAsync(dbEmployee, NewPassword);
                        }
                        else if (dbEmployee.UserName != Login)
                        {
                            await _userManager.SetUserNameAsync(dbEmployee, Login);
                            if (Login.Contains("@")) await _userManager.SetEmailAsync(dbEmployee, Login);
                        }
                    }

                    var existingApps = _context.StaffAppointments.Where(a => a.EmployeeId == id);
                    _context.StaffAppointments.RemoveRange(existingApps);

                    if (selectedPositions != null && selectedDepartments != null)
                    {
                        for (int i = 0; i < selectedPositions.Length; i++)
                        {
                            if (i < selectedDepartments.Length)
                            {
                                _context.StaffAppointments.Add(new StaffAppointment
                                {
                                    Id = Guid.NewGuid(),
                                    EmployeeId = id,
                                    PositionId = selectedPositions[i],
                                    DepartmentId = selectedDepartments[i],
                                    IsPrimary = (i == 0)
                                });
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeExists(employee.Id)) return NotFound(); else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            await LoadDynamicFields("Employee");
            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View(employee);
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