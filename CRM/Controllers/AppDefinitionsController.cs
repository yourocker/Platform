using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Data;
using Core.Entities.Platform;
using Core.Entities.Platform.Form;
using CRM.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRM.Controllers;

public class AppDefinitionsController(AppDbContext context) : Controller
{
    private readonly AppDbContext _context = context;

    public async Task<IActionResult> Index(string search, string? f_Search, int pageNumber = 1, int pageSize = 10, string systemFilter = "all")
    {
        if (string.IsNullOrWhiteSpace(search) && !string.IsNullOrWhiteSpace(f_Search))
        {
            search = f_Search;
        }
        // 1. Начинаем формирование запроса
        var query = _context.AppDefinitions
            .Include(a => a.Fields)
            .Include(a => a.Category)
            .AsQueryable();

        // 1.1. Быстрый фильтр по системности
        systemFilter = (systemFilter ?? "all").Trim().ToLowerInvariant();
        if (systemFilter == "system") query = query.Where(a => a.IsSystem);
        else if (systemFilter == "user") query = query.Where(a => !a.IsSystem);
        else systemFilter = "all";

        // 2. Логика поиска (фильтруем по имени или коду)
        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(a => a.Name.ToLower().Contains(searchLower) || 
                                     a.EntityCode.ToLower().Contains(searchLower));
        }

        // 3. Считаем общее количество ПОСЛЕ фильтрации
        var totalItems = await query.CountAsync();

        // 4. Пагинация и сортировка
        var apps = await query
            .OrderBy(a => a.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 5. Наполнение ViewBag для вьюхи (строго по эталону)
        ViewBag.TotalItems = totalItems;
        ViewBag.PageNumber = pageNumber;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        ViewBag.CurrentSearch = search;
        ViewBag.SystemFilter = systemFilter;

        return View(apps);
    }

    [HttpGet]
    public async Task<IActionResult> Create(bool modal = false)
    {
        var categories = await _context.AppCategories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Categories = new SelectList(categories, "Id", "Name");
        ViewBag.IsModal = modal;

        var model = new AppDefinition { Icon = "box" };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppDefinition app, bool modal = false)
    {
        if (ModelState.IsValid)
        {
            app.Id = Guid.NewGuid();
            if (string.IsNullOrEmpty(app.Icon)) app.Icon = "box";

            _context.AppDefinitions.Add(app);
            await _context.SaveChangesAsync();

            if (!app.IsSystem)
            {
                await EnsureNameFieldAsync(app.Id);
                await EnsureDefaultFormsAsync(app.Id);
            }
            if (modal)
            {
                return BuildModalCreatedContentResult("AppDefinition", app.Id, app.Name);
            }
            return RedirectToAction(nameof(Index));
        }

        var categories = await _context.AppCategories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Categories = new SelectList(categories, "Id", "Name");
        ViewBag.IsModal = modal;
        
        return View(app);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var app = await _context.AppDefinitions.FindAsync(id);
        if (app == null) return NotFound();

        var categories = await _context.AppCategories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Categories = new SelectList(categories, "Id", "Name", app.AppCategoryId);
        
        return View(app);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, AppDefinition app)
    {
        if (id != app.Id) return NotFound();

        if (ModelState.IsValid)
        {
            _context.Update(app);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        var categories = await _context.AppCategories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Categories = new SelectList(categories, "Id", "Name", app.AppCategoryId);
        return View(app);
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id)
    {
        var app = await _context.AppDefinitions
            .Include(a => a.Category)
            .FirstOrDefaultAsync(m => m.Id == id);
            
        if (app == null) return NotFound();

        return View(app);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var app = await _context.AppDefinitions.FindAsync(id);
        if (app != null)
        {
            var fields = _context.AppFieldDefinitions.Where(f => f.AppDefinitionId == id);
            _context.AppFieldDefinitions.RemoveRange(fields);
            
            _context.AppDefinitions.Remove(app);
            await _context.SaveChangesAsync();
        }
        
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Fields(Guid id)
    {
        var app = await _context.AppDefinitions
            .Include(a => a.Fields)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (app == null) return NotFound();

        ViewBag.AllDefinitions = await _context.AppDefinitions
            .OrderBy(a => a.Name)
            .ToListAsync();

        return View(app);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddField(
        Guid appDefinitionId,
        string label,
        string systemName,
        FieldDataType dataType,
        bool isRequired,
        bool isArray,
        string? targetEntityCode,
        List<string>? optionLabels,
        List<string>? optionValues)
    {
        var app = await _context.AppDefinitions
            .Include(a => a.Fields)
            .FirstOrDefaultAsync(a => a.Id == appDefinitionId);

        if (app == null) return NotFound();

        var normalizedSystemName = systemName?.Trim().ToLower();

        if (string.IsNullOrWhiteSpace(label))
        {
            TempData["Error"] = "Название поля не может быть пустым.";
            return RedirectToAction(nameof(Fields), new { id = appDefinitionId });
        }

        if (string.IsNullOrEmpty(normalizedSystemName))
        {
            TempData["Error"] = "Системное имя не может быть пустым.";
            return RedirectToAction(nameof(Fields), new { id = appDefinitionId });
        }

        if (!Regex.IsMatch(normalizedSystemName, @"^[a-z][a-z0-9_]*$"))
        {
            TempData["Error"] = "Системное имя должно содержать только латиницу, цифры и начинаться с буквы.";
            return RedirectToAction(nameof(Fields), new { id = appDefinitionId });
        }

        var isDuplicate = await _context.AppFieldDefinitions
            .AnyAsync(f => f.AppDefinitionId == appDefinitionId && f.SystemName.ToLower() == normalizedSystemName);

        if (isDuplicate)
        {
            TempData["Error"] = $"Поле с системным именем '{normalizedSystemName}' уже существует в этой сущности.";
            return RedirectToAction(nameof(Fields), new { id = appDefinitionId });
        }

        int nextSortOrder = app.Fields.Any() ? app.Fields.Max(f => f.SortOrder) + 1 : 0;

        var newField = new AppFieldDefinition
        {
            Id = Guid.NewGuid(),
            AppDefinitionId = appDefinitionId,
            Label = label.Trim(),
            SystemName = normalizedSystemName,
            SortOrder = nextSortOrder
        };

        var configurationError = ApplyFieldConfiguration(
            newField,
            dataType,
            isRequired,
            isArray,
            targetEntityCode,
            optionLabels,
            optionValues);

        if (!string.IsNullOrWhiteSpace(configurationError))
        {
            TempData["Error"] = configurationError;
            return RedirectToAction(nameof(Fields), new { id = appDefinitionId });
        }

        _context.AppFieldDefinitions.Add(newField);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Fields), new { id = appDefinitionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditField(
        Guid id,
        string label,
        FieldDataType dataType,
        bool isRequired,
        bool isArray,
        string? targetEntityCode,
        List<string>? optionLabels,
        List<string>? optionValues)
    {
        var field = await _context.AppFieldDefinitions.FindAsync(id);
        if (field == null) return NotFound();

        if (string.IsNullOrWhiteSpace(label))
        {
            TempData["Error"] = "Название поля не может быть пустым.";
            return RedirectToAction(nameof(Fields), new { id = field.AppDefinitionId });
        }

        field.Label = label.Trim();

        var configurationError = ApplyFieldConfiguration(
            field,
            dataType,
            isRequired,
            isArray,
            targetEntityCode,
            optionLabels,
            optionValues);

        if (!string.IsNullOrWhiteSpace(configurationError))
        {
            TempData["Error"] = configurationError;
            return RedirectToAction(nameof(Fields), new { id = field.AppDefinitionId });
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Fields), new { id = field.AppDefinitionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteField(Guid id)
    {
        var field = await _context.AppFieldDefinitions.FindAsync(id);
        if (field == null) return NotFound();

        if (field.IsSystem)
        {
            TempData["Error"] = "Системные поля удалять нельзя.";
            return RedirectToAction(nameof(Fields), new { id = field.AppDefinitionId });
        }

        var appId = field.AppDefinitionId;
        _context.AppFieldDefinitions.Remove(field);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Fields), new { id = appId });
    }

    private async Task EnsureNameFieldAsync(Guid appDefinitionId)
    {
        var exists = await _context.AppFieldDefinitions
            .AnyAsync(f => f.AppDefinitionId == appDefinitionId && f.SystemName.ToLower() == "name");
        if (exists) return;

        _context.AppFieldDefinitions.Add(new AppFieldDefinition
        {
            Id = Guid.NewGuid(),
            AppDefinitionId = appDefinitionId,
            Label = "Название",
            SystemName = "Name",
            DataType = FieldDataType.String,
            IsRequired = true,
            IsSystem = true,
            SortOrder = 0
        });
        await _context.SaveChangesAsync();
    }

    private async Task EnsureDefaultFormsAsync(Guid appDefinitionId)
    {
        var nameFieldId = await _context.AppFieldDefinitions
            .Where(f => f.AppDefinitionId == appDefinitionId)
            .Where(f => f.SystemName.ToLower() == "name")
            .Select(f => f.Id)
            .FirstOrDefaultAsync();

        if (nameFieldId == Guid.Empty) return;

        var existing = await _context.AppFormDefinitions
            .Where(f => f.AppDefinitionId == appDefinitionId)
            .ToListAsync();

        bool changed = false;
        foreach (var type in Enum.GetValues<Core.Entities.Platform.Form.FormType>())
        {
            var formsOfType = existing.Where(f => f.Type == type).ToList();
            if (!formsOfType.Any())
            {
                _context.AppFormDefinitions.Add(new AppFormDefinition
                {
                    Id = Guid.NewGuid(),
                    AppDefinitionId = appDefinitionId,
                    Name = "Основная форма",
                    Type = type,
                    IsDefault = true,
                    Layout = BuildNameOnlyLayout(nameFieldId)
                });
                changed = true;
                continue;
            }

            if (!formsOfType.Any(f => f.IsDefault))
            {
                formsOfType.First().IsDefault = true;
                changed = true;
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }
    }

    private static string BuildNameOnlyLayout(Guid nameFieldId)
    {
        return $"{{\"nodes\":[{{\"type\":\"field\",\"FieldId\":\"{nameFieldId}\"}}]}}";
    }

    private static string? ApplyFieldConfiguration(
        AppFieldDefinition field,
        FieldDataType dataType,
        bool isRequired,
        bool isArray,
        string? targetEntityCode,
        IReadOnlyList<string>? optionLabels,
        IReadOnlyList<string>? optionValues)
    {
        field.DataType = dataType;
        field.IsRequired = isRequired;
        field.IsArray = isArray;

        if (dataType == FieldDataType.EntityLink)
        {
            if (string.IsNullOrWhiteSpace(targetEntityCode))
            {
                return "Для поля-связи нужно выбрать целевую сущность.";
            }

            field.TargetEntityCode = targetEntityCode.Trim();
            field.SetSelectOptions(null);
            return null;
        }

        field.TargetEntityCode = null;

        if (dataType != FieldDataType.Select)
        {
            field.SetSelectOptions(null);
            return null;
        }

        var options = BuildSelectOptions(optionLabels, optionValues);
        if (options.Count == 0)
        {
            return "Для выпадающего списка нужно добавить хотя бы один пункт.";
        }

        if (options.Count != options.Select(option => option.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return "Пункты выпадающего списка не должны дублироваться.";
        }

        field.SetSelectOptions(options);
        return null;
    }

    private static List<FieldSelectOption> BuildSelectOptions(
        IReadOnlyList<string>? optionLabels,
        IReadOnlyList<string>? optionValues)
    {
        var labels = optionLabels ?? Array.Empty<string>();
        var values = optionValues ?? Array.Empty<string>();
        var maxCount = Math.Max(labels.Count, values.Count);
        var options = new List<FieldSelectOption>();

        for (var i = 0; i < maxCount; i++)
        {
            var label = i < labels.Count ? labels[i]?.Trim() : null;
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var value = i < values.Count ? values[i]?.Trim() : null;
            options.Add(new FieldSelectOption
            {
                Value = string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value,
                Label = label,
                SortOrder = options.Count
            });
        }

        return options;
    }

    private ContentResult BuildModalCreatedContentResult(string entityCode, Guid id, string? name)
    {
        return ModalRequestHelper.BuildEntityCreatedContent(entityCode, id, name);
    }
    
    // GET: AppDefinitions/FormBuilder/{id}
    // Единственная точка входа конструктора форм: возвращаем modal-версию.
    [HttpGet]
    public async Task<IActionResult> FormBuilder(Guid id)
    {
        var appDef = await _context.AppDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        if (appDef == null) return NotFound();

        await EnsureDefaultFormsAsync(id);

        // 1. Загружаем все существующие конфигурации форм для этой сущности
        var existingForms = await _context.AppFormDefinitions
            .Where(f => f.AppDefinitionId == id)
            .OrderBy(f => f.Type)
            .ThenByDescending(f => f.IsDefault)
            .ThenBy(f => f.Name)
            .ToListAsync();

        // 2. Готовим ViewModel
        var viewModel = new CRM.ViewModels.FormConfig.FormBuilderViewModel
        {
            AppDefinitionId = appDef.Id,
            AppDefinitionName = appDef.Name,
            EntityCode = appDef.EntityCode
        };

        // 3. Передаем все формы (для выбора/редактирования)
        viewModel.Forms = existingForms.Select(f => new CRM.ViewModels.FormConfig.FormBuilderFormDto
        {
            Id = f.Id,
            Name = f.Name,
            Type = f.Type,
            IsDefault = f.IsDefault,
            LayoutJson = string.IsNullOrWhiteSpace(f.Layout) ? "{\"nodes\":[]}" : f.Layout
        }).ToList();

        ViewBag.AllDefinitions = await _context.AppDefinitions
            .AsNoTracking()
            .OrderBy(definition => definition.Name)
            .ToListAsync();

        // Возвращаем только modal-версию конструктора (single source of truth)
        return PartialView("FormBuilderModal", viewModel);
    }
}
