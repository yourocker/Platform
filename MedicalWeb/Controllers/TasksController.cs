using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedicalBot.Data;
using MedicalBot.Entities.Tasks;
using MedicalBot.Entities.Company;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MedicalWeb.Controllers
{
    public class TasksController : BasePlatformController
    {
        public TasksController(AppDbContext context, IWebHostEnvironment hostingEnvironment) 
            : base(context, hostingEnvironment)
        {
        }

        // Вспомогательный класс для корректной сериализации объектов
        private class ObjectLookupDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
        }

        public async Task<IActionResult> Index()
        {
            var tasks = await _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Include(t => t.Relations) // ОБЯЗАТЕЛЬНО: загружаем связи
                .Where(t => !t.IsDeleted)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            await ResolveRelatedEntityNames(tasks);
            return View(tasks);
        }

        public async Task<IActionResult> CreatedByMe()
        {
            var user = await GetCurrentUser();
            var tasks = await _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Include(t => t.Relations)
                .Where(t => t.AuthorId == user.Id && !t.IsDeleted)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            await ResolveRelatedEntityNames(tasks);
            ViewData["Title"] = "Поставленные мной";
            return View("Index", tasks);
        }

        public async Task<IActionResult> AssignedToMe()
        {
            var user = await GetCurrentUser();
            var tasks = await _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Include(t => t.Relations)
                .Where(t => t.AssigneeId == user.Id && !t.IsDeleted)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            await ResolveRelatedEntityNames(tasks);
            ViewData["Title"] = "Назначенные мне";
            return View("Index", tasks);
        }

        // Остальные методы (CompletedTasks, OverdueTasks) обновляются аналогично добавлением .Include(t => t.Relations)

        public async Task<IActionResult> Create()
        {
            await PrepareViewBags();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeTask task, string[] selectedObjects)
        {
            if (ModelState.IsValid)
            {
                task.Id = Guid.NewGuid();
                task.CreatedAt = DateTime.UtcNow;
                task.Status = MedicalBot.Entities.Tasks.TaskStatus.Created;

                // Обработка динамических связей
                if (selectedObjects != null)
                {
                    foreach (var item in selectedObjects.Where(s => !string.IsNullOrEmpty(s)))
                    {
                        var parts = item.Split('|');
                        if (parts.Length == 2)
                        {
                            task.Relations.Add(new TaskEntityRelation
                            {
                                EntityCode = parts[0],
                                EntityId = Guid.Parse(parts[1])
                            });
                        }
                    }
                }

                _context.Add(task);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await PrepareViewBags();
            return View(task);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var task = await _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Include(t => t.Relations) // Загружаем связи
                .Include(t => t.Comments.OrderBy(c => c.CreatedAt)).ThenInclude(c => c.Author)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (task == null) return NotFound();
            await ResolveRelatedEntityNames(new List<EmployeeTask> { task });
            return View(task);
        }

        private async Task PrepareViewBags()
        {
            // 1. Сотрудники для ролей
            ViewBag.Employees = await _context.Employees
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
                .ToListAsync();

            // 2. Все типы сущностей из определений системы
            // Используем .ToList() перед Select, чтобы избежать проблем с трансляцией имен в SQL
            var definitions = await _context.AppDefinitions.ToListAsync();
            ViewBag.EntityTypes = definitions
                .Select(d => new SelectListItem 
                { 
                    Value = d.EntityCode, 
                    Text = d.Name 
                })
                .ToList();

            // 3. ЗАГРУЗКА ВСЕХ ОБЪЕКТОВ (включая Должности, Пациентов и т.д.)
            // Тянем данные напрямую из GenericObjects, так как там лежат все динамические сущности
            var allObjects = await _context.GenericObjects
                .Select(o => new ObjectLookupDto
                { 
                    Id = o.Id, 
                    Name = o.Name ?? "Без названия", 
                    Type = o.EntityCode 
                })
                .ToListAsync();
    
            ViewBag.AllObjectsJson = System.Text.Json.JsonSerializer.Serialize(allObjects);
        } 

        private async Task<Employee> GetCurrentUser()
        {
            return await _context.Employees.FirstAsync();
        }
    }
}