using MedicalBot.Data;
using MedicalBot.Entities.Company;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MedicalWeb.Controllers
{
    public class DepartmentsController : Controller
    {
        private readonly AppDbContext _context;
        public DepartmentsController(AppDbContext context) { _context = context; }

        // Список всех отделов
        // Список всех отделов
        public async Task<IActionResult> Index()
        {
            // 1. Загружаем отделы со всеми связями (сотрудники и их данные)
            var departments = await _context.Departments
                .Include(d => d.StaffAppointments).ThenInclude(a => a.Employee)
                .Include(d => d.Manager) 
                .ToListAsync();

            // 2. Загружаем список работающих сотрудников для выпадающего списка
            var staff = await _context.Employees
                .Where(e => !e.IsDismissed)
                .OrderBy(e => e.LastName)
                .Select(e => new { e.Id, FullName = e.LastName + " " + e.FirstName + " " + e.MiddleName })
                .ToListAsync();

            // 3. ЗАГРУЖАЕМ СПИСОК ДОЛЖНОСТЕЙ (Чтобы не было ошибки в базе)
            var positions = await _context.Positions
                .OrderBy(p => p.Name)
                .ToListAsync();

            // 4. Передаем всё это во Вьюху через ViewBag
            ViewBag.Staff = new SelectList(staff, "Id", "FullName");
            ViewBag.Positions = new SelectList(positions, "Id", "Name"); 

            return View(departments);
        }

        // Создание: Страница (GET)
        public async Task<IActionResult> Create()
        {
            ViewBag.ParentDepartments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name");
    
            // Подгружаем активных сотрудников для выпадающего списка
            var staff = await _context.Employees
                .Where(e => !e.IsDismissed)
                .OrderBy(e => e.LastName)
                .Select(e => new { e.Id, FullName = e.LastName + " " + e.FirstName + " " + e.MiddleName })
                .ToListAsync();
    
            ViewBag.Staff = new SelectList(staff, "Id", "FullName");
            return View();
        }

        // Создание: Сохранение (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Department department)
        {
            if (ModelState.IsValid)
            {
                department.Id = Guid.NewGuid();
                _context.Add(department);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.ParentDepartments = new SelectList(await _context.Departments.ToListAsync(), "Id", "Name");
            return View(department);
        }
        
        // Редактирование: Страница (GET)
        public async Task<IActionResult> Edit(Guid id)
        {
            var department = await _context.Departments.FindAsync(id);
            if (department == null) return NotFound();

            var otherDepartments = await _context.Departments
                .Where(d => d.Id != id)
                .OrderBy(d => d.Name)
                .ToListAsync();

            ViewBag.ParentDepartments = new SelectList(otherDepartments, "Id", "Name", department.ParentId);

            // Подгружаем активных сотрудников для выпадающего списка
            var staff = await _context.Employees
                .Where(e => !e.IsDismissed)
                .OrderBy(e => e.LastName)
                .Select(e => new { e.Id, FullName = e.LastName + " " + e.FirstName + " " + e.MiddleName })
                .ToListAsync();

            ViewBag.Staff = new SelectList(staff, "Id", "FullName", department.ManagerId);
            return View(department);
        }

        // Редактирование: Сохранение (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Department department)
        {
            if (id != department.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(department);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(department);
        }
        
        // Удаление: (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            // Загружаем отдел вместе со списком сотрудников
            var department = await _context.Departments
                .Include(d => d.StaffAppointments)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department == null) return NotFound();

            // 1. Проверка на наличие нижестоящих отделов
            var hasChildren = await _context.Departments.AnyAsync(d => d.ParentId == id);
            if (hasChildren)
            {
                TempData["Error"] = "Нельзя удалить отдел, у которого есть дочерние подразделения!";
                return RedirectToAction(nameof(Index));
            }

            // 2. Проверка на наличие сотрудников
            if (department.StaffAppointments != null && department.StaffAppointments.Any())
            {
                TempData["Error"] = "В отделе числятся сотрудники! Сначала переведите их в другое подразделение.";
                return RedirectToAction(nameof(Index));
            }

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignEmployee(Guid departmentId, Guid employeeId, Guid positionId)
        {
            // Проверяем, нет ли уже точно такой же записи (чтобы не дублировать)
            var exists = await _context.StaffAppointments
                .AnyAsync(a => a.DepartmentId == departmentId && 
                               a.EmployeeId == employeeId && 
                               a.PositionId == positionId);

            if (!exists)
            {
                var appointment = new StaffAppointment
                {
                    Id = Guid.NewGuid(),
                    DepartmentId = departmentId,
                    EmployeeId = employeeId,
                    PositionId = positionId // Теперь используем ID должности из формы
                };
        
                _context.StaffAppointments.Add(appointment);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}