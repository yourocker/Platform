using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core.Data;
using Core.Entities.Tasks;
using Core.Entities.Company;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace CRM.Controllers
{
    [Authorize]
    public class TasksController : BasePlatformController
    {
        public TasksController(AppDbContext context, IWebHostEnvironment hostingEnvironment) 
            : base(context, hostingEnvironment)
        {
        }

        private class ObjectLookupDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
        }

        // --- СПИСКИ (VIEWS) ---

        public async Task<IActionResult> Index()
        {
            ViewBag.FilterType = "All";
            var tasks = await _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Include(t => t.Relations)
                .Where(t => !t.IsDeleted && t.Status != Core.Entities.Tasks.TaskStatus.Completed)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            await ResolveRelatedEntityNames(tasks);
            return View(tasks);
        }

        public async Task<IActionResult> CreatedByMe()
        {
            var user = await GetCurrentUser();
            ViewBag.FilterType = "CreatedByMe";
            
            var tasks = await _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Include(t => t.Relations)
                .Where(t => t.AuthorId == user.Id 
                            && !t.IsDeleted 
                            && t.Status != Core.Entities.Tasks.TaskStatus.Completed)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            await ResolveRelatedEntityNames(tasks);
            ViewData["Title"] = "Поставленные мной";
            return View("Index", tasks);
        }

        public async Task<IActionResult> AssignedToMe()
        {
            var user = await GetCurrentUser();
            ViewBag.FilterType = "AssignedToMe";

            var tasks = await _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Include(t => t.Relations)
                .Where(t => t.AssigneeId == user.Id 
                            && !t.IsDeleted 
                            && t.Status != Core.Entities.Tasks.TaskStatus.Completed)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            await ResolveRelatedEntityNames(tasks);
            ViewData["Title"] = "Назначенные мне";
            return View("Index", tasks);
        }
        
        // ИСПРАВЛЕНО: CompletedTasks вместо Completed
        public async Task<IActionResult> CompletedTasks()
        {
            var user = await GetCurrentUser();
            ViewBag.FilterType = "Completed";

            var tasks = await _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Include(t => t.Relations)
                .Where(t => (t.AuthorId == user.Id || t.AssigneeId == user.Id)
                            && t.Status == Core.Entities.Tasks.TaskStatus.Completed 
                            && !t.IsDeleted)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            await ResolveRelatedEntityNames(tasks);
            ViewData["Title"] = "Выполненные задачи";
            return View("Index", tasks);
        }

        // ИСПРАВЛЕНО: OverdueTasks вместо Overdue
        public async Task<IActionResult> OverdueTasks()
        {
            var user = await GetCurrentUser();
            ViewBag.FilterType = "Overdue";

            var tasks = await _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Include(t => t.Relations)
                .Where(t => (t.AuthorId == user.Id || t.AssigneeId == user.Id)
                            && t.Deadline < DateTime.UtcNow
                            && t.Status != Core.Entities.Tasks.TaskStatus.Completed
                            && !t.IsDeleted)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            await ResolveRelatedEntityNames(tasks);
            ViewData["Title"] = "Просроченные задачи";
            return View("Index", tasks);
        }
        
        // --- ПРОСМОТР И ДЕЙСТВИЯ ---

        public async Task<IActionResult> Details(Guid id)
        {
            var user = await GetCurrentUser();
            var task = await _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Include(t => t.Relations)
                .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
                    .ThenInclude(c => c.Author) 
                .FirstOrDefaultAsync(m => m.Id == id);

            if (task == null) return NotFound();

            ViewBag.CurrentUserId = user.Id;
            await ResolveRelatedEntityNames(new List<EmployeeTask> { task });
            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(Guid taskId, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return RedirectToAction("Details", new { id = taskId });

            var user = await GetCurrentUser();
            
            var comment = new TaskComment
            {
                TaskId = taskId,
                AuthorId = user.Id,
                Text = text,
                CreatedAt = DateTime.UtcNow
            };

            _context.Add(comment);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = taskId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(Guid id, Core.Entities.Tasks.TaskStatus status)
        {
            var task = await _context.EmployeeTasks.FindAsync(id);
            if (task == null) return NotFound();

            var user = await GetCurrentUser();
            // Разрешаем менять статус автору или исполнителю
            if (task.AuthorId != user.Id && task.AssigneeId != user.Id)
            {
                return Forbid();
            }

            task.Status = status;
            _context.Update(task);
            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTask(Guid id)
        {
            var task = await _context.EmployeeTasks.FindAsync(id);
            if (task == null) return NotFound();
            
            var user = await GetCurrentUser();
            if (task.AuthorId != user.Id) return Forbid();

            task.IsDeleted = true;
            task.DeletedAt = DateTime.UtcNow;
            
            _context.Update(task);
            await _context.SaveChangesAsync();
            
            return RedirectToAction("Index");
        }

        // --- СОЗДАНИЕ ---

        public async Task<IActionResult> Create()
        {
            var user = await GetCurrentUser();
            ViewBag.CurrentUserId = user.Id;
            ViewBag.CurrentUserName = user.FullName;
            await PrepareViewBags();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeTask task, string[] selectedObjects)
        {
            var user = await GetCurrentUser();
            
            task.AuthorId = user.Id;
            task.Id = Guid.NewGuid();
            task.CreatedAt = DateTime.UtcNow;
            task.Status = Core.Entities.Tasks.TaskStatus.Created;
            task.Name = task.Title; 
            task.EntityCode = "EmployeeTask"; 

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

            ModelState.Remove("Author");
            ModelState.Remove("AuthorId");
            ModelState.Remove("Assignee");
            ModelState.Remove("Name");
            ModelState.Remove("EntityCode");

            if (ModelState.IsValid)
            {
                _context.Add(task);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(CreatedByMe));
            }
            
            ViewBag.CurrentUserId = user.Id;
            ViewBag.CurrentUserName = user.FullName;
            await PrepareViewBags();
            return View(task);
        }

        // --- API ---

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(Guid id, int status)
        {
            var task = await _context.EmployeeTasks.FindAsync(id);
            if (task == null) return NotFound(new { message = "Задача не найдена" });
            task.Status = (Core.Entities.Tasks.TaskStatus)status;
            _context.Update(task);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetCalendarEvents(string filter = "All")
        {
            var user = await GetCurrentUser();
            var query = _context.EmployeeTasks.Include(t => t.Assignee).Where(t => !t.IsDeleted);

            switch (filter)
            {
                case "CreatedByMe":
                    query = query.Where(t => t.AuthorId == user.Id && t.Status != Core.Entities.Tasks.TaskStatus.Completed); break;
                case "AssignedToMe":
                    query = query.Where(t => t.AssigneeId == user.Id && t.Status != Core.Entities.Tasks.TaskStatus.Completed); break;
                case "Completed":
                    query = query.Where(t => (t.AuthorId == user.Id || t.AssigneeId == user.Id) && t.Status == Core.Entities.Tasks.TaskStatus.Completed); break;
                case "Overdue":
                    query = query.Where(t => (t.AuthorId == user.Id || t.AssigneeId == user.Id) && t.Deadline < DateTime.UtcNow && t.Status != Core.Entities.Tasks.TaskStatus.Completed); break;
                default:
                    query = query.Where(t => t.Status != Core.Entities.Tasks.TaskStatus.Completed); break;
            }

            var tasks = await query.ToListAsync();
            var events = tasks.Select(t => new
            {
                id = t.Id,
                title = t.Title,
                start = t.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = t.Deadline.HasValue ? t.Deadline.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                backgroundColor = GetColorForStatus(t.Status),
                borderColor = GetColorForStatus(t.Status),
                url = Url.Action("Details", new { id = t.Id }),
                extendedProps = new { assignee = t.Assignee?.FullName ?? "Не назначен" }
            });
            return Json(events);
        }

        private async Task PrepareViewBags()
        {
            ViewBag.Employees = await _context.Employees.Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName }).ToListAsync();
            var definitions = await _context.AppDefinitions.ToListAsync();
            ViewBag.EntityTypes = definitions.Select(d => new SelectListItem { Value = d.EntityCode, Text = d.Name }).ToList();
            var allObjects = await _context.GenericObjects.Select(o => new ObjectLookupDto { Id = o.Id, Name = o.Name ?? "Без названия", Type = o.EntityCode }).ToListAsync();
            ViewBag.AllObjectsJson = System.Text.Json.JsonSerializer.Serialize(allObjects);
        } 

        private async Task<Employee> GetCurrentUser()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrEmpty(email)) throw new Exception("User not authorized");
            
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Email == email);
            if (employee == null) 
            {
                 return await _context.Employees.FirstAsync(); 
            }
            return employee;
        }

        private string GetColorForStatus(Core.Entities.Tasks.TaskStatus status)
        {
            return status switch
            {
                Core.Entities.Tasks.TaskStatus.Created => "#6c757d",
                Core.Entities.Tasks.TaskStatus.InProgress => "#0d6efd",
                Core.Entities.Tasks.TaskStatus.InReview => "#fd7e14",
                Core.Entities.Tasks.TaskStatus.Completed => "#198754",
                _ => "#6c757d"
            };
        }
    }
}