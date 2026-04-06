using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core.Data;
using Core.Entities.Tasks;
using Core.Entities.Company;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using CRM.ViewModels.Filters;
using CRM.Infrastructure;

namespace CRM.Controllers
{
    [Authorize]
    public class TasksController : BasePlatformController
    {
        public TasksController(AppDbContext context, IWebHostEnvironment hostingEnvironment) 
            : base(context, hostingEnvironment)
        {
        }

        private IQueryable<EmployeeTask> BuildTaskQuery(string filterType, Guid currentUserId)
        {
            var query = _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Where(t => !t.IsDeleted);

            return filterType switch
            {
                "CreatedByMe" => query.Where(t => t.AuthorId == currentUserId && t.Status != Core.Entities.Tasks.TaskStatus.Completed),
                "AssignedToMe" => query.Where(t => t.AssigneeId == currentUserId && t.Status != Core.Entities.Tasks.TaskStatus.Completed),
                "Completed" => query.Where(t => (t.AuthorId == currentUserId || t.AssigneeId == currentUserId) && t.Status == Core.Entities.Tasks.TaskStatus.Completed),
                "Overdue" => query.Where(t => (t.AuthorId == currentUserId || t.AssigneeId == currentUserId) && t.Deadline < DateTime.UtcNow && t.Status != Core.Entities.Tasks.TaskStatus.Completed),
                _ => query.Where(t => t.Status != Core.Entities.Tasks.TaskStatus.Completed)
            };
        }

        private static IQueryable<EmployeeTask> ApplyTaskFilters(
            IQueryable<EmployeeTask> query,
            string? searchString,
            IDictionary<string, string>? filters)
        {
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var search = searchString.Trim();
                query = query.Where(t =>
                    EF.Functions.ILike(t.Title, $"%{search}%") ||
                    (t.Description != null && EF.Functions.ILike(t.Description, $"%{search}%")) ||
                    EF.Functions.ILike(t.Author.FullName, $"%{search}%") ||
                    EF.Functions.ILike(t.Assignee.FullName, $"%{search}%"));
            }

            if (filters == null || !filters.Any())
            {
                return query;
            }

            if (filters.TryGetValue("f_Status", out var statusRaw) &&
                int.TryParse(statusRaw, out var statusValue))
            {
                query = query.Where(t => (int)t.Status == statusValue);
            }

            if (filters.TryGetValue("f_AuthorId", out var authorRaw) &&
                Guid.TryParse(authorRaw, out var authorId))
            {
                query = query.Where(t => t.AuthorId == authorId);
            }

            if (filters.TryGetValue("f_AssigneeId", out var assigneeRaw) &&
                Guid.TryParse(assigneeRaw, out var assigneeId))
            {
                query = query.Where(t => t.AssigneeId == assigneeId);
            }

            if (filters.TryGetValue("f_DeadlineFrom", out var deadlineFromRaw) &&
                DateTime.TryParse(deadlineFromRaw, out var deadlineFrom))
            {
                var fromDate = deadlineFrom.Date;
                query = query.Where(t => t.Deadline.HasValue && t.Deadline.Value >= fromDate);
            }

            if (filters.TryGetValue("f_DeadlineTo", out var deadlineToRaw) &&
                DateTime.TryParse(deadlineToRaw, out var deadlineTo))
            {
                var toDateExclusive = deadlineTo.Date.AddDays(1);
                query = query.Where(t => t.Deadline.HasValue && t.Deadline.Value < toDateExclusive);
            }

            return query;
        }

        private FilterPanelViewModel BuildTaskFilterPanelModel(
            IEnumerable<SelectListItem> employees,
            IDictionary<string, string> currentFilters)
        {
            var statusOptions = new List<FilterOptionViewModel>
            {
                new() { Value = ((int)Core.Entities.Tasks.TaskStatus.Created).ToString(), Label = "Новая" },
                new() { Value = ((int)Core.Entities.Tasks.TaskStatus.InProgress).ToString(), Label = "В работе" },
                new() { Value = ((int)Core.Entities.Tasks.TaskStatus.InReview).ToString(), Label = "На проверке" },
                new() { Value = ((int)Core.Entities.Tasks.TaskStatus.Completed).ToString(), Label = "Готова" }
            };

            var employeeOptions = employees
                .Select(item => new FilterOptionViewModel { Value = item.Value, Label = item.Text })
                .ToList();

            return new FilterPanelViewModel
            {
                ActionUrl = Url.Action(RouteData.Values["Action"]?.ToString() ?? nameof(Index)) ?? "/Tasks",
                ResetUrl = Url.Action(RouteData.Values["Action"]?.ToString() ?? nameof(Index)) ?? "/Tasks",
                EntityCode = "EmployeeTask",
                ViewCode = RouteData.Values["Action"]?.ToString() ?? "Index",
                SearchValue = ViewBag.CurrentSearch as string ?? string.Empty,
                SearchPlaceholder = "Быстрый поиск",
                PageSize = 20,
                ExpandedByDefault = currentFilters.Any(),
                Fields = new List<FilterFieldViewModel>
                {
                    new() { Key = "f_Status", Label = "Статус", Kind = FilterInputKind.Select, Value = TryGetFilterValue(currentFilters, "f_Status"), Options = statusOptions },
                    new() { Key = "f_AuthorId", Label = "Постановщик", Kind = FilterInputKind.EntityLink, Value = TryGetFilterValue(currentFilters, "f_AuthorId"), Options = employeeOptions },
                    new() { Key = "f_AssigneeId", Label = "Исполнитель", Kind = FilterInputKind.EntityLink, Value = TryGetFilterValue(currentFilters, "f_AssigneeId"), Options = employeeOptions },
                    new() { Key = "f_DeadlineFrom", Label = "Срок с", Kind = FilterInputKind.Date, Value = TryGetFilterValue(currentFilters, "f_DeadlineFrom") },
                    new() { Key = "f_DeadlineTo", Label = "Срок по", Kind = FilterInputKind.Date, Value = TryGetFilterValue(currentFilters, "f_DeadlineTo") }
                }
            };
        }

        private async Task<IActionResult> RenderTaskIndex(string filterType, string? searchString, Dictionary<string, string>? filters)
        {
            var user = await GetCurrentUser();
            var employees = await _context.Employees
                .AsNoTracking()
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
                .ToListAsync();

            filters ??= new Dictionary<string, string>();

            var tasks = await ApplyTaskFilters(BuildTaskQuery(filterType, user.Id), searchString, filters)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            ViewBag.FilterType = filterType;
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentFilters = filters;
            ViewBag.FilterPanelModel = BuildTaskFilterPanelModel(employees, filters);
            ViewData["Title"] = filterType switch
            {
                "CreatedByMe" => "Поставленные мной",
                "AssignedToMe" => "Назначенные мне",
                "Completed" => "Выполненные",
                "Overdue" => "Просроченные задачи",
                _ => "Все задачи"
            };

            return View("Index", tasks);
        }

        // --- СПИСКИ (VIEWS) ---

        public async Task<IActionResult> Index(string? searchString, [FromQuery] Dictionary<string, string>? filters = null)
        {
            return await RenderTaskIndex("All", searchString, filters);
        }

        public async Task<IActionResult> CreatedByMe(string? searchString, [FromQuery] Dictionary<string, string>? filters = null)
        {
            return await RenderTaskIndex("CreatedByMe", searchString, filters);
        }

        public async Task<IActionResult> AssignedToMe(string? searchString, [FromQuery] Dictionary<string, string>? filters = null)
        {
            return await RenderTaskIndex("AssignedToMe", searchString, filters);
        }
        
        public async Task<IActionResult> CompletedTasks(string? searchString, [FromQuery] Dictionary<string, string>? filters = null)
        {
            return await RenderTaskIndex("Completed", searchString, filters);
        }

        public async Task<IActionResult> OverdueTasks(string? searchString, [FromQuery] Dictionary<string, string>? filters = null)
        {
            return await RenderTaskIndex("Overdue", searchString, filters);
        }
        
        // --- ПРОСМОТР И ДЕЙСТВИЯ ---

        public async Task<IActionResult> Details(Guid id, bool modal = false)
        {
            var user = await GetCurrentUser();
            var task = await _context.EmployeeTasks
                .Include(t => t.Author)
                .Include(t => t.Assignee)
                .Include(t => t.Comments.OrderBy(c => c.CreatedAt))
                    .ThenInclude(c => c.Author) 
                .FirstOrDefaultAsync(m => m.Id == id);

            if (task == null) return NotFound();

            ViewBag.CurrentUserId = user.Id;
            ViewBag.IsModal = modal;
            return View(task);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(Guid taskId, string text, bool modal = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return modal
                    ? ModalRequestHelper.BuildRedirectContent(Url.Action(nameof(Details), new { id = taskId, modal = true }))
                    : RedirectToAction(nameof(Details), new { id = taskId });
            }

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

            return modal
                ? ModalRequestHelper.BuildRedirectContent(Url.Action(nameof(Details), new { id = taskId, modal = true }))
                : RedirectToAction(nameof(Details), new { id = taskId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeStatus(Guid id, Core.Entities.Tasks.TaskStatus status, bool modal = false)
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
            return modal
                ? ModalRequestHelper.BuildRedirectContent(Url.Action(nameof(Details), new { id, modal = true }))
                : RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTask(Guid id, bool modal = false)
        {
            var task = await _context.EmployeeTasks.FindAsync(id);
            if (task == null) return NotFound();
            
            var user = await GetCurrentUser();
            if (task.AuthorId != user.Id) return Forbid();

            task.IsDeleted = true;
            task.DeletedAt = DateTime.UtcNow;
            
            _context.Update(task);
            await _context.SaveChangesAsync();

            return modal
                ? ModalRequestHelper.BuildRefreshContent()
                : RedirectToAction(nameof(Index));
        }

        // --- СОЗДАНИЕ ---

        public async Task<IActionResult> Create(bool modal = false)
        {
            var user = await GetCurrentUser();
            ViewBag.CurrentUserName = user.FullName;
            ViewBag.IsModal = modal;
            await PrepareViewBags();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeTask task, bool modal = false)
        {
            var user = await GetCurrentUser();
            
            task.AuthorId = user.Id;
            task.Id = Guid.NewGuid();
            task.CreatedAt = DateTime.UtcNow;
            task.Status = Core.Entities.Tasks.TaskStatus.Created;
            task.Name = task.Title; 
            task.EntityCode = "EmployeeTask"; 

            ModelState.Remove("Author");
            ModelState.Remove("AuthorId");
            ModelState.Remove("Assignee");
            ModelState.Remove("Name");
            ModelState.Remove("EntityCode");

            if (ModelState.IsValid)
            {
                _context.Add(task);
                await _context.SaveChangesAsync();

                if (modal)
                {
                    return BuildModalCreatedContentResult("EmployeeTask", task.Id, task.Title);
                }

                return RedirectToAction(nameof(CreatedByMe));
            }
            
            ViewBag.CurrentUserName = user.FullName;
            ViewBag.IsModal = modal;
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
        }

        private async Task<Employee> GetCurrentUser()
        {
            var employeeId = TryGetCurrentEmployeeId();
            if (!employeeId.HasValue)
            {
                throw new InvalidOperationException("Не удалось определить пользователя текущей сессии.");
            }

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId.Value);

            if (employee == null)
            {
                throw new InvalidOperationException("Пользователь текущей сессии не найден среди сотрудников.");
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
