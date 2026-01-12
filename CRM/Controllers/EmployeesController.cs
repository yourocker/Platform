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

        // --- СПИСОК СОТРУДНИКОВ ---
        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .Include(e => e.StaffAppointments)
                .ThenInclude(a => a.Position)
                .Include(e => e.StaffAppointments)
                .ThenInclude(a => a.Department)
                .OrderBy(e => e.IsDismissed) 
                .ThenBy(e => e.LastName)
                .ToListAsync();
            return View(employees);
        }

        // --- СОЗДАНИЕ (GET) ---
        public async Task<IActionResult> Create()
        {
            await LoadDynamicFields("Employee"); 
            
            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View();
        }

        // --- СОЗДАНИЕ (POST) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee, Guid[] selectedPositions, Guid[] selectedDepartments, string[] Phones, string[] Emails, IFormCollection form, string? Login, string? Password)
        {
            employee.Phones = Phones?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
            employee.Emails = Emails?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();

            // 1. Сохраняем во временную папку
            await SaveDynamicProperties(employee, form, "Employee");

            // Убираем поля логина/пароля из валидации, так как они не часть сущности при привязке
            ModelState.Remove("Login");
            ModelState.Remove("Password");

            if (ModelState.IsValid)
            {
                employee.Id = Guid.NewGuid();

                // === ЛОГИКА СОЗДАНИЯ ПОЛЬЗОВАТЕЛЯ IDENTITY ===
                bool createdViaIdentity = false;
                if (!string.IsNullOrEmpty(Login) && !string.IsNullOrEmpty(Password))
                {
                    employee.UserName = Login;
                    // Если логин похож на Email, записываем и туда
                    employee.Email = Login.Contains("@") ? Login : null;

                    var result = await _userManager.CreateAsync(employee, Password);
                    
                    if (!result.Succeeded)
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                        // Если ошибка создания пользователя - возвращаем форму
                        await LoadDynamicFields("Employee");
                        ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
                        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
                        return View(employee);
                    }
                    createdViaIdentity = true;
                }
                else
                {
                    // Обычное создание без доступа в систему
                    _context.Add(employee);
                    await _context.SaveChangesAsync();
                }
                // ==============================================

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

                // 2. Сохраняем связи (StaffAppointments)
                // Если создавали через Identity, сотрудник уже в БД, сохраняем только связи
                await _context.SaveChangesAsync();

                // 3. Перемещаем файлы из Temp в постоянную папку
                FinalizeDynamicFilePaths(employee, "Employee", employee.Id.ToString());

                // 4. Обновляем пути в БД
                // IdentityDbContext отслеживает изменения, поэтому SaveChangesAsync обновит и свойства сотрудника
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            await LoadDynamicFields("Employee");
            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View(employee);
        }

        // --- РЕДАКТИРОВАНИЕ (GET) ---
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees
                .Include(e => e.StaffAppointments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null) return NotFound();

            await LoadDynamicFields("Employee");
            ViewBag.Departments = await _context.Departments.ToListAsync();
            ViewBag.Positions = await _context.Positions.ToListAsync();

            // --- ДОБАВЛЕНО: Проверка Identity ---
            // Проверяем, есть ли такой пользователь в Identity (по Id или UserName)
            // Так как Employee наследует IdentityUser, поля UserName и Email уже в объекте employee
            // Но SecurityStamp может быть null, если пользователь создан без Identity
            ViewBag.HasLogin = !string.IsNullOrEmpty(employee.UserName);
            // ------------------------------------

            return View(employee);
        }

        // --- РЕДАКТИРОВАНИЕ (POST) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Employee employee, Guid[] selectedPositions, Guid[] selectedDepartments, string[] Phones, string[] Emails, IFormCollection form, string? Login, string? NewPassword)
        {
            if (id != employee.Id) return NotFound();

            await SaveDynamicProperties(employee, form, "Employee");
            
            // Убираем валидацию полей, которые мы обрабатываем вручную
            ModelState.Remove("Login");
            ModelState.Remove("NewPassword");

            if (ModelState.IsValid)
            {
                try
                {
                    FinalizeDynamicFilePaths(employee, "Employee", employee.Id.ToString());

                    var dbEmployee = await _context.Employees.FindAsync(id);
                    if (dbEmployee == null) return NotFound();

                    // 1. Обновляем обычные поля
                    dbEmployee.FirstName = employee.FirstName;
                    dbEmployee.LastName = employee.LastName;
                    dbEmployee.MiddleName = employee.MiddleName;
                    dbEmployee.IsDismissed = employee.IsDismissed;
                    dbEmployee.Properties = employee.Properties;
                    
                    dbEmployee.Phones = Phones?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
                    dbEmployee.Emails = Emails?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();

                    // 2. Логика Identity (Вход в систему)
                    // Если логин передан и отличается — меняем
                    if (!string.IsNullOrEmpty(Login))
                    {
                        // Если юзера еще не было в Identity (UserName пустой), и задан пароль — создаем/активируем
                        if (string.IsNullOrEmpty(dbEmployee.UserName) && !string.IsNullOrEmpty(NewPassword))
                        {
                            dbEmployee.UserName = Login;
                            dbEmployee.Email = Login.Contains("@") ? Login : null;
                            await _userManager.UpdateNormalizedUserNameAsync(dbEmployee);
                            await _userManager.UpdateNormalizedEmailAsync(dbEmployee);
                            await _userManager.AddPasswordAsync(dbEmployee, NewPassword);
                        }
                        else 
                        {
                            // Просто смена логина
                            if (dbEmployee.UserName != Login)
                            {
                                await _userManager.SetUserNameAsync(dbEmployee, Login);
                                if (Login.Contains("@"))
                                {
                                    await _userManager.SetEmailAsync(dbEmployee, Login);
                                }
                            }
                        }
                    }

                    // 3. Обновляем должности
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
                    if (!EmployeeExists(employee.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await LoadDynamicFields("Employee");
            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View(employee);
        }

        // --- НОВЫЙ МЕТОД: ПРИНУДИТЕЛЬНАЯ СМЕНА ПАРОЛЯ АДМИНОМ ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminResetPassword(Guid id, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound("Сотрудник не найден");

            // Удаляем старый пароль, если он был
            if (await _userManager.HasPasswordAsync(user))
            {
                await _userManager.RemovePasswordAsync(user);
            }
            
            // Ставим новый
            var result = await _userManager.AddPasswordAsync(user, newPassword);
            
            if (result.Succeeded)
            {
                return Ok();
            }
            return BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        private bool EmployeeExists(Guid id)
        {
            return _context.Employees.Any(e => e.Id == id);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Dismiss(Guid id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null)
            {
                employee.IsDismissed = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Edit), new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(Guid id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee != null)
            {
                employee.IsDismissed = false;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Edit), new { id = id });
        }
    }
}