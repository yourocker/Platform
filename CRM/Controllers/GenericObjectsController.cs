using System;
using System.Linq;
using System.Threading.Tasks;
using Core.Data;
using Core.Entities.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
//using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Core.Entities.Platform.Form;
using System.Text.Json;
using Core.Entities.CRM;
using Core.Interfaces.Platform;
using CRM.Infrastructure;

namespace CRM.Controllers;

// Устанавливаем базовый маршрут для всех действий контроллера
[Route("Data")]
public class GenericObjectsController(
    AppDbContext context,
    IWebHostEnvironment hostingEnvironment,
    IEntityTimelineService timelineService) : BasePlatformController(context, hostingEnvironment)
{
    private readonly IEntityTimelineService _timelineService = timelineService;

    private async Task LoadDefinitionWithFields(string entityCode, FormType? formType = null)
    {
        var definition = await _context.AppDefinitions
            .Include(d => d.Fields)
            .FirstOrDefaultAsync(d => d.EntityCode == entityCode);

        if (definition != null)
        {
            ViewBag.DynamicFields = definition.Fields.OrderBy(f => f.SortOrder).ToList();
            ViewBag.DefinitionName = definition.Name;
            ViewBag.HasNameInLayout = false;
            ViewBag.LookupData = await BuildEntityLinkLookupDataAsync(definition.Fields);

            if (formType.HasValue)
            {
                var layout = await LoadDefaultFormLayout(definition.Id, formType.Value);
                ViewBag.FormLayout = layout;

                var nameFieldId = definition.Fields.FirstOrDefault(f => f.SystemName == "Name")?.Id;
                if (layout != null && nameFieldId.HasValue)
                {
                    ViewBag.HasNameInLayout = LayoutContainsField(layout.Nodes, nameFieldId.Value);
                }
            }
        }
    }

    private static bool LayoutContainsField(IEnumerable<LayoutNode> nodes, Guid fieldId)
    {
        foreach (var node in nodes ?? Array.Empty<LayoutNode>())
        {
            if (node is FieldNode fn && fn.FieldId == fieldId) return true;
            if (node is TabControlNode tcn && (tcn.Tabs ?? new List<TabNode>()).Any(t => LayoutContainsField(t.Children, fieldId))) return true;
            if (node is TabNode tn && LayoutContainsField(tn.Children, fieldId)) return true;
            if (node is GroupNode gn && LayoutContainsField(gn.Children, fieldId)) return true;
            if (node is RowNode rn && (rn.Columns ?? new List<ColumnNode>()).Any(c => LayoutContainsField(c.Children, fieldId))) return true;
            if (node is ColumnNode cn && LayoutContainsField(cn.Children, fieldId)) return true;
        }
        return false;
    }

    private async Task<FormLayoutSchema?> LoadDefaultFormLayout(Guid appDefinitionId, FormType formType)
    {
        var forms = await _context.AppFormDefinitions
            .AsNoTracking()
            .Where(f => f.AppDefinitionId == appDefinitionId && f.Type == formType)
            .OrderByDescending(f => f.IsDefault)
            .ThenBy(f => f.Name)
            .ToListAsync();

        if (!forms.Any()) return null;

        foreach (var form in forms)
        {
            if (string.IsNullOrWhiteSpace(form.Layout)) continue;

            try
            {
                var layout = FormLayoutSchema.TryParse(form.Layout);

                if (layout?.Nodes != null && layout.Nodes.Any())
                {
                    if (!form.IsDefault)
                    {
                        var tracked = await _context.AppFormDefinitions.FindAsync(form.Id);
                        if (tracked != null && !tracked.IsDefault)
                        {
                            tracked.IsDefault = true;
                            await _context.SaveChangesAsync();
                        }
                    }
                    return layout;
                }
            }
            catch
            {
                // пропускаем некорректные макеты
            }
        }

        return null;
    }

    // Маршрут: /Data/{entityCode} (например /Data/Sklad)
    [HttpGet("{entityCode}")]
    public async Task<IActionResult> Index(string entityCode, string? searchString, int? pageNumber, int? pageSize, [FromQuery] Dictionary<string, string>? filters)
    {
        if (string.IsNullOrEmpty(entityCode)) return NotFound();
        
        var definition = await _context.AppDefinitions
            .Include(d => d.Fields)
            .FirstOrDefaultAsync(d => d.EntityCode == entityCode);

        if (definition == null) return NotFound();

        ViewBag.Definition = definition;
        ViewBag.EntityCode = entityCode;
        ViewBag.DynamicFields = definition.Fields.OrderBy(f => f.SortOrder).ToList();

        var query = _context.GenericObjects
            .Where(o => o.EntityCode == entityCode)
            .AsQueryable();

        // 1. Быстрый поиск по системному имени
        if (!string.IsNullOrWhiteSpace(searchString))
        {
            query = query.Where(o => EF.Functions.ILike(o.Name, $"%{searchString}%"));
        }

        // 2. Тотальная фильтрация по динамическим полям (Properties)
        if (filters != null && filters.Any())
        {
            foreach (var filter in filters)
            {
                if (string.IsNullOrWhiteSpace(filter.Value)) continue;

                if (filter.Key.Contains("f_Name"))
                {
                    query = query.Where(o => EF.Functions.ILike(o.Name, $"%{filter.Value}%"));
                }
                else if (filter.Key.Contains("f_dyn_"))
                {
                    var fieldName = filter.Key.Split("f_dyn_").Last();
                    query = query.Where(o => EF.Functions.ILike(o.Properties, $"%\"{fieldName}\":%\"{filter.Value}\"%"));
                }
            }
        }

        // 3. Честная пагинация
        int actualPageSize = pageSize ?? 10;
        int actualPageNumber = pageNumber ?? 1;
        int totalItems = await query.CountAsync();

        var objects = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((actualPageNumber - 1) * actualPageSize)
            .Take(actualPageSize)
            .ToListAsync();

        // 4. Сбор GUID для текущей страницы (твоя логика Regex)
        var allGuids = new HashSet<Guid>();
        var guidRegex = new Regex(@"([a-fA-F0-9]{8}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{12})");

        foreach (var obj in objects.Where(o => !string.IsNullOrEmpty(o.Properties)))
        {
            var matches = guidRegex.Matches(obj.Properties);
            foreach (Match match in matches)
            {
                if (Guid.TryParse(match.Value, out Guid g)) allGuids.Add(g);
            }
        }

        var namesMap = new Dictionary<Guid, string>();
        if (allGuids.Count > 0)
        {
            var genericNames = await _context.GenericObjects.Where(g => allGuids.Contains(g.Id)).Select(g => new { g.Id, g.Name }).ToListAsync();
            foreach (var n in genericNames) namesMap[n.Id] = n.Name;

            var employeeNames = await _context.Employees.Where(e => allGuids.Contains(e.Id)).Select(e => new { e.Id, Name = e.FullName }).ToListAsync();
            foreach (var n in employeeNames) namesMap[n.Id] = n.Name;

            var deptNames = await _context.Departments.Where(d => allGuids.Contains(d.Id)).Select(d => new { d.Id, d.Name }).ToListAsync();
            foreach (var n in deptNames) namesMap[n.Id] = n.Name;
        }

        ViewBag.NamesMap = namesMap;
        ViewBag.TotalItems = totalItems;
        ViewBag.PageNumber = actualPageNumber;
        ViewBag.PageSize = actualPageSize;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)actualPageSize);
        ViewBag.CurrentSearch = searchString;
        ViewBag.CurrentFilters = filters ?? new Dictionary<string, string>();

        return View(objects);
    }

    // Маршрут: /Data/{entityCode}/Create
    [HttpGet("{entityCode}/Create")]
    public async Task<IActionResult> Create(string entityCode, bool modal = false)
    {
        if (string.IsNullOrEmpty(entityCode)) return NotFound();
        await LoadDefinitionWithFields(entityCode, FormType.Create);
        ViewBag.EntityCode = entityCode;
        ViewBag.IsModal = modal;
        return View();
    }

    [HttpPost("{entityCode}/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string entityCode, GenericObject obj, IFormCollection form, bool modal = false)
    {
        obj.EntityCode = entityCode;
        obj.CreatedAt = DateTime.UtcNow;
        await SaveDynamicProperties(obj, form, obj.EntityCode);
        if (ModelState.IsValid)
        {
            obj.Id = Guid.NewGuid();
            _context.Add(obj);
            await _context.SaveChangesAsync();
            FinalizeDynamicFilePaths(obj, obj.EntityCode, obj.Id.ToString());
            await _context.SaveChangesAsync();

            await _timelineService.LogEventAsync(
                obj.Id,
                obj.EntityCode,
                CrmEventType.System,
                "Объект создан",
                $"Создан объект \"{obj.Name}\".",
                TryGetCurrentEmployeeId());

            if (modal)
            {
                return BuildModalCreatedContentResult(obj.EntityCode, obj.Id, obj.Name);
            }

            return Redirect($"/Data/{obj.EntityCode}");
        }
        await LoadDefinitionWithFields(obj.EntityCode, FormType.Create);
        ViewBag.EntityCode = obj.EntityCode;
        ViewBag.IsModal = modal;
        return View(obj);
    }
    
    // Маршрут: /Data/Edit/{id}
    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var obj = await _context.GenericObjects.FindAsync(id);
        if (obj == null) return NotFound();
        await LoadDefinitionWithFields(obj.EntityCode, FormType.Edit);
        ViewBag.EntityCode = obj.EntityCode;
        return View(obj);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, GenericObject incomingObj, IFormCollection form)
    {
        if (id != incomingObj.Id) return NotFound();
        var dbObj = await _context.GenericObjects.FindAsync(id);
        if (dbObj == null) return NotFound();

        var beforeName = dbObj.Name;
        var beforeProps = TimelineChangeFormatter.ParseDynamicProperties(dbObj.Properties);

        dbObj.Name = incomingObj.Name;
        await SaveDynamicProperties(dbObj, form, dbObj.EntityCode);
        if (ModelState.IsValid)
        {
            try 
            {
                FinalizeDynamicFilePaths(dbObj, dbObj.EntityCode, dbObj.Id.ToString());
                _context.Update(dbObj);
                await _context.SaveChangesAsync();

                var fieldLabels = await LoadFieldLabelMapAsync(dbObj.EntityCode);
                var afterProps = TimelineChangeFormatter.ParseDynamicProperties(dbObj.Properties);
                var changeSummary = BuildGenericObjectChangeSummary(beforeName, dbObj.Name, beforeProps, afterProps, fieldLabels);

                await _timelineService.LogEventAsync(
                    dbObj.Id,
                    dbObj.EntityCode,
                    CrmEventType.FieldChange,
                    "Объект обновлён",
                    changeSummary,
                    TryGetCurrentEmployeeId());
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.GenericObjects.Any(e => e.Id == id)) return NotFound();
                else throw;
            }
            return Redirect($"/Data/{dbObj.EntityCode}");
        }
        await LoadDefinitionWithFields(dbObj.EntityCode, FormType.Edit);
        ViewBag.EntityCode = dbObj.EntityCode;
        return View(dbObj);
    }

    [HttpGet("Details/{id}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var obj = await _context.GenericObjects.FindAsync(id);
        if (obj == null) return NotFound();

        await LoadDefinitionWithFields(obj.EntityCode, FormType.View);
        ViewBag.TimelineEvents = await _timelineService.GetEventsAsync(obj.Id, obj.EntityCode);
        ViewBag.EntityCode = obj.EntityCode;
        return View(obj);
    }

    // Маршрут: /Data/Delete/{id}
    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var obj = await _context.GenericObjects.FindAsync(id);
        if (obj == null) return NotFound();
        var entityCode = obj.EntityCode;
        var objectName = obj.Name;
        _context.GenericObjects.Remove(obj);
        await _context.SaveChangesAsync();

        await _timelineService.LogEventAsync(
            id,
            entityCode,
            CrmEventType.System,
            "Объект удалён",
            $"Удалён объект \"{objectName}\".",
            TryGetCurrentEmployeeId());

        return Redirect($"/Data/{entityCode}");
    }

    private static string? BuildGenericObjectChangeSummary(
        string? beforeName,
        string? afterName,
        IReadOnlyDictionary<string, string> beforeProps,
        IReadOnlyDictionary<string, string> afterProps,
        IReadOnlyDictionary<string, string> fieldLabels)
    {
        var changes = new List<string>();

        TimelineChangeFormatter.AddScalarChange(changes, "Наименование", beforeName, afterName);
        TimelineChangeFormatter.AddDictionaryChanges(
            changes,
            beforeProps,
            afterProps,
            key => fieldLabels.TryGetValue(key, out var label) ? label : key);

        return TimelineChangeFormatter.BuildSummary(changes);
    }
}
