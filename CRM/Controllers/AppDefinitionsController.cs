using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions; // Добавлено для проверки Regex
using Core.Data;
using Core.Entities.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRM.Controllers;

public class AppDefinitionsController(AppDbContext context) : Controller
{
    private readonly AppDbContext _context = context;

    public async Task<IActionResult> Index()
    {
        var apps = await _context.AppDefinitions
            .Include(a => a.Fields)
            .Include(a => a.Category)
            .OrderBy(a => a.Name)
            .ToListAsync();
        return View(apps);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        // Загружаем категории для выпадающего списка в форме
        var categories = await _context.AppCategories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Categories = new SelectList(categories, "Id", "Name");

        // Создаем модель с начальными значениями (иконка по умолчанию)
        var model = new AppDefinition { Icon = "box" };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppDefinition app)
    {
        if (ModelState.IsValid)
        {
            app.Id = Guid.NewGuid();
            
            // Если иконка не выбрана, ставим стандартную
            if (string.IsNullOrEmpty(app.Icon)) app.Icon = "box";

            _context.AppDefinitions.Add(app);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Если валидация не прошла, возвращаем категории обратно во вьюху
        var categories = await _context.AppCategories.OrderBy(c => c.SortOrder).ToListAsync();
        ViewBag.Categories = new SelectList(categories, "Id", "Name");
        
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
    public async Task<IActionResult> AddField(Guid appDefinitionId, string label, string systemName, FieldDataType dataType, bool isRequired, bool isArray, string? targetEntityCode)
    {
        var app = await _context.AppDefinitions
            .Include(a => a.Fields)
            .FirstOrDefaultAsync(a => a.Id == appDefinitionId);

        if (app == null) return NotFound();

        var normalizedSystemName = systemName?.Trim().ToLower();

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
            Label = label,
            SystemName = normalizedSystemName,
            DataType = dataType,
            IsRequired = isRequired,
            IsArray = isArray,
            SortOrder = nextSortOrder,
            TargetEntityCode = targetEntityCode
        };

        _context.AppFieldDefinitions.Add(newField);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Fields), new { id = appDefinitionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteField(Guid id)
    {
        var field = await _context.AppFieldDefinitions.FindAsync(id);
        if (field == null) return NotFound();

        var appId = field.AppDefinitionId;
        _context.AppFieldDefinitions.Remove(field);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Fields), new { id = appId });
    }
}