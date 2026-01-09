using MedicalBot.Data;
using MedicalBot.Entities.Company;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MedicalWeb.Controllers
{
    public class DepartmentsController : BasePlatformController
    {
        public DepartmentsController(AppDbContext context) : base(context)
        {
        }

        public async Task<IActionResult> Index()
        {
            var departments = await _context.Departments
                .Include(d => d.Manager)
                .Include(d => d.Parent)
                .Include(d => d.StaffAppointments)
                    .ThenInclude(sa => sa.Employee)
                .OrderBy(d => d.Name)
                .ToListAsync();

            return View(departments);
        }

        public async Task<IActionResult> Create()
        {
            await LoadDynamicFields("Department");
            await LoadLookupLists();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Department department, Dictionary<string, string> dynamicProps)
        {
            SaveDynamicProperties(department, dynamicProps);

            if (ModelState.IsValid)
            {
                department.Id = Guid.NewGuid();
                _context.Add(department);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            
            await LoadDynamicFields("Department");
            await LoadLookupLists();
            return View(department);
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var department = await _context.Departments.FindAsync(id);
            if (department == null) return NotFound();

            await LoadDynamicFields("Department");
            await LoadLookupLists(department.Id);
            return View(department);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Department department, Dictionary<string, string> dynamicProps)
        {
            if (id != department.Id) return NotFound();

            SaveDynamicProperties(department, dynamicProps);

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(department);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DepartmentExists(department.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            
            await LoadDynamicFields("Department");
            await LoadLookupLists(department.Id);
            return View(department);
        }

        // GET: Departments/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var department = await _context.Departments
                .FirstOrDefaultAsync(m => m.Id == id);
    
            if (department == null)
            {
                return NotFound();
            }

            // Проверка наличия АКТИВНЫХ сотрудников (тех, у кого IsDismissed == false)
            bool hasActiveEmployees = await _context.StaffAppointments
                .AnyAsync(sa => sa.DepartmentId == id && sa.Employee != null && !sa.Employee.IsDismissed);
    
            // Проверка наличия дочерних подразделений
            bool hasSubDepartments = await _context.Departments
                .AnyAsync(d => d.ParentId == id);

            if (hasActiveEmployees || hasSubDepartments)
            {
                ViewBag.CanDelete = false;
                ViewBag.Reason = hasActiveEmployees 
                    ? "В подразделении есть действующие сотрудники. Удаление невозможно." 
                    : "У данного подразделения есть дочерние структуры. Удаление невозможно.";
            }
            else
            {
                ViewBag.CanDelete = true;
            }

            return View(department);
        }

        // POST: Departments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var department = await _context.Departments
                .Include(d => d.StaffAppointments)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (department != null)
            {
                // Если в подразделении остались только уволенные сотрудники, 
                // удаляем их связи (назначения), чтобы "очистить" им место работы
                if (department.StaffAppointments != null && department.StaffAppointments.Any())
                {
                    _context.StaffAppointments.RemoveRange(department.StaffAppointments);
                }

                _context.Departments.Remove(department);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadLookupLists(Guid? currentDeptId = null)
        {
            var employees = await _context.Employees
                .OrderBy(e => e.LastName)
                .Select(e => new { Id = e.Id, FullName = $"{e.LastName} {e.FirstName} {e.MiddleName}" })
                .ToListAsync();
            ViewBag.ManagerId = new SelectList(employees, "Id", "FullName");

            var parentsQuery = _context.Departments.AsQueryable();
            if (currentDeptId.HasValue)
                parentsQuery = parentsQuery.Where(d => d.Id != currentDeptId.Value);
            
            var parents = await parentsQuery.OrderBy(d => d.Name).ToListAsync();
            ViewBag.ParentId = new SelectList(parents, "Id", "Name");
        }

        private bool DepartmentExists(Guid id) => _context.Departments.Any(e => e.Id == id);
    }
}