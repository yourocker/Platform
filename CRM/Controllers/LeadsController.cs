using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core.Data;
using Core.Entities.CRM;
using Core.Interfaces.CRM;
using CRM.ViewModels.CRM;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using System.Security.Claims;

namespace CRM.Controllers
{
    public class LeadsController : BasePlatformController
    {
        private readonly AppDbContext _context;
        private readonly ICrmService _crmService;

        public LeadsController(AppDbContext context, IWebHostEnvironment hostingEnvironment, ICrmService crmService) 
            : base(context, hostingEnvironment)
        {
            _context = context;
            _crmService = crmService;
        }

        public async Task<IActionResult> Index(Guid? pipelineId, string view = "kanban", string searchString = "", int pageNumber = 1, int pageSize = 10)
{
    // 1. Нормализуем режим отображения
    view = string.IsNullOrEmpty(view) ? "kanban" : view.ToLower().Trim();

    var pipelines = await _context.CrmPipelines.Where(p => p.TargetEntityCode == "Lead" && p.IsActive).ToListAsync();
    var currentPipeline = pipelineId.HasValue 
        ? pipelines.FirstOrDefault(p => p.Id == pipelineId) 
        : pipelines.OrderBy(p => p.SortOrder).FirstOrDefault();

    if (currentPipeline == null) return NotFound("Воронка не найдена.");

    var stages = await _context.CrmStages.Where(s => s.PipelineId == currentPipeline.Id).OrderBy(s => s.SortOrder).ToListAsync();
    var appDef = await _context.AppDefinitions.Include(a => a.Fields).FirstOrDefaultAsync(a => a.EntityCode == "Lead");
    var dynamicFields = appDef?.Fields.OrderBy(f => f.SortOrder).ToList() ?? new();

    var query = _context.Leads.Where(l => l.PipelineId == currentPipeline.Id && !l.IsConverted).AsQueryable();
    
    // Фильтрация
    if (!string.IsNullOrEmpty(searchString))
    {
        var s = searchString.Trim();
        query = query.Where(l => EF.Functions.ILike(l.Name, $"%{s}%") || (l.Properties != null && EF.Functions.ILike((string)(object)l.Properties, $"%{s}%")));
    }

    var totalItems = await query.CountAsync();
    
    // 2. В режиме Канбан отключаем пагинацию (берем топ-500 для доски), в Списке оставляем как есть
    List<Lead> leads;
    if (view == "kanban") {
        leads = await query.Include(l => l.CurrentStage).OrderByDescending(l => l.CreatedAt).Take(500).ToListAsync();
    } else {
        leads = await query.Include(l => l.CurrentStage).OrderByDescending(l => l.CreatedAt)
                           .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    return View(new LeadsViewModel { 
        CurrentPipeline = currentPipeline, 
        AllPipelines = pipelines, 
        Stages = stages, 
        Leads = leads, 
        DynamicFields = dynamicFields, 
        ViewMode = view, // Здесь всегда lowercase
        SearchString = searchString, 
        PageNumber = pageNumber, 
        PageSize = pageSize, 
        TotalItems = totalItems 
    });
}

        public async Task<IActionResult> Details(Guid id)
        {
            var lead = await _context.Leads.Include(l => l.CurrentStage).Include(l => l.Responsible).Include(l => l.Contact).FirstOrDefaultAsync(m => m.Id == id);
            if (lead == null) return NotFound();

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid? currentUserId = string.IsNullOrEmpty(userIdString) ? null : Guid.Parse(userIdString);

            await _crmService.LogEventAsync(id, "Lead", CrmEventType.View, "Просмотр карточки", null, currentUserId);

            ViewBag.TimelineEvents = await _context.CrmEvents.Include(e => e.Employee).Where(e => e.TargetId == id && e.Type != CrmEventType.View).OrderByDescending(e => e.IsPinned).ThenByDescending(e => e.CreatedAt).ToListAsync();
            ViewBag.HistoryEvents = await _context.CrmEvents.Include(e => e.Employee).Where(e => e.TargetId == id).OrderByDescending(e => e.CreatedAt).ToListAsync();

            return View(lead);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeStage(Guid leadId, Guid newStageId)
        {
            var success = await _crmService.ChangeStageAsync(leadId, "Lead", newStageId);
            return success ? Ok() : BadRequest("Ошибка перехода.");
        }

        // НОВЫЙ МЕТОД: ОБРАБОТКА ИНЛАЙН-РЕДАКТИРОВАНИЯ (ШАГ 4)
        [HttpPost]
        public async Task<IActionResult> UpdateProperty(Guid id, string name, string value)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid currentUserId = string.IsNullOrEmpty(userIdString) ? Guid.Empty : Guid.Parse(userIdString);

            var success = await _crmService.UpdatePropertyAsync(id, "Lead", name, value, currentUserId);
            return success ? Json(new { success = true }) : Json(new { success = false, message = "Не удалось обновить поле" });
        }
    }
}