using MedicalBot.Data;
using MedicalBot.Entities.Company;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json; // Нужно для работы с JSON

namespace MedicalWeb.Controllers
{
    public class EmployeesController : Controller
    {
        private readonly AppDbContext _context;

        public EmployeesController(AppDbContext context)
        {
            _context = context;
        }

        // Вспомогательный метод, чтобы не дублировать код загрузки полей
        private async Task LoadDynamicFields()
        {
            var dynamicFields = await _context.AppFieldDefinitions
                .Where(f => f.AppDefinition.SystemCode == "Employee")
                .OrderBy(f => f.SortOrder)
                .ToListAsync();
            ViewBag.DynamicFields = dynamicFields;
        }

        // 1. СПИСОК СОТРУДНИКОВ
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

        // 2. СОЗДАНИЕ: СТРАНИЦА (GET)
        public async Task<IActionResult> Create()
        {
            await LoadDynamicFields(); // Загружаем поля для формы создания
            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View();
        }

        // 3. СОЗДАНИЕ: СОХРАНЕНИЕ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee, Guid[] selectedPositions, Guid[] selectedDepartments, string[] Phones, string[] Emails, Dictionary<string, string> DynamicProps)
        {
            employee.Phones = Phones?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
            employee.Emails = Emails?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();

            // Сохраняем динамические поля в JSON
            if (DynamicProps != null && DynamicProps.Any())
            {
                employee.Properties = JsonSerializer.Serialize(DynamicProps);
            }

            if (ModelState.IsValid)
            {
                employee.Id = Guid.NewGuid();
                _context.Add(employee);

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
                return RedirectToAction(nameof(Index));
            }

            await LoadDynamicFields();
            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View(employee);
        }

        // 4. РЕДАКТИРОВАНИЕ: СТРАНИЦА (GET)
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var employee = await _context.Employees
                .Include(e => e.StaffAppointments)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null) return NotFound();

            await LoadDynamicFields(); // Загружаем поля для формы редактирования

            ViewBag.Departments = await _context.Departments.ToListAsync();
            ViewBag.Positions = await _context.Positions.ToListAsync();

            return View(employee);
        }

        // 5. РЕДАКТИРОВАНИЕ: СОХРАНЕНИЕ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Employee employee, Guid[] selectedPositions, Guid[] selectedDepartments, string[] Phones, string[] Emails, Dictionary<string, string> DynamicProps)
        {
            if (id != employee.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    employee.Phones = Phones?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
                    employee.Emails = Emails?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();
                    
                    // Сохраняем динамические поля в JSON
                    if (DynamicProps != null && DynamicProps.Any())
                    {
                        employee.Properties = JsonSerializer.Serialize(DynamicProps);
                    }

                    _context.Update(employee);

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

            await LoadDynamicFields();
            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View(employee);
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