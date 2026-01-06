using MedicalBot.Data;
using MedicalBot.Entities.Company;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalWeb.Controllers
{
    public class EmployeesController : Controller
    {
        private readonly AppDbContext _context;

        public EmployeesController(AppDbContext context)
        {
            _context = context;
        }

        // 1. СПИСОК СОТРУДНИКОВ
        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .Include(e => e.StaffAppointments)
                .ThenInclude(a => a.Position)
                .Include(e => e.StaffAppointments)
                .ThenInclude(a => a.Department)
                // Сначала работающие (false), потом уволенные (true)
                .OrderBy(e => e.IsDismissed) 
                .ThenBy(e => e.LastName)
                .ToListAsync();
            return View(employees);
        }

        // 2. СОЗДАНИЕ: СТРАНИЦА (GET)
        public async Task<IActionResult> Create()
        {
            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View();
        }

        // 3. СОЗДАНИЕ: СОХРАНЕНИЕ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee employee, Guid[] selectedPositions, Guid[] selectedDepartments, string[] Phones, string[] Emails)
        {
            employee.Phones = Phones?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
            employee.Emails = Emails?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();

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

            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View(employee);
        }

        // 4. РЕДАКТИРОВАНИЕ: СТРАНИЦА (GET)
        public async Task<IActionResult> Edit(Guid id)
        {
            var employee = await _context.Employees
                .Include(e => e.StaffAppointments)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View(employee);
        }

        // 5. РЕДАКТИРОВАНИЕ: СОХРАНЕНИЕ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Employee employee, Guid[] selectedPositions, Guid[] selectedDepartments, string[] Phones, string[] Emails)
        {
            if (id != employee.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Обновляем базовые поля и контакты
                    employee.Phones = Phones?.Where(p => !string.IsNullOrWhiteSpace(p)).ToList() ?? new List<string>();
                    employee.Emails = Emails?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList() ?? new List<string>();
                    
                    _context.Update(employee);

                    // Обновляем назначения: удаляем старые, пишем новые
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

            ViewBag.Positions = await _context.Positions.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            return View(employee);
        }

        private bool EmployeeExists(Guid id)
        {
            return _context.Employees.Any(e => e.Id == id);
        }
        
        // Метод для увольнения
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

// Метод для восстановления
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