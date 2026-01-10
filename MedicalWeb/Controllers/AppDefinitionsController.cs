using System;
using MedicalBot.Data;
using MedicalBot.Entities.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;

namespace MedicalWeb.Controllers;

public class AppDefinitionsController(AppDbContext context) : Controller
{
    private readonly AppDbContext _context = context;

    public async Task<IActionResult> Index()
    {
        var apps = await _context.AppDefinitions
            .Include(a => a.Fields)
            .OrderBy(a => a.Name)
            .ToListAsync();
        return View(apps);
    }

    public async Task<IActionResult> Fields(Guid id)
    {
        var app = await _context.AppDefinitions
            .Include(a => a.Fields)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (app == null) return NotFound();

        return View(app);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddField(Guid appDefinitionId, string label, string systemName, FieldDataType dataType, bool isRequired, bool isArray)
    {
        var app = await _context.AppDefinitions
            .Include(a => a.Fields)
            .FirstOrDefaultAsync(a => a.Id == appDefinitionId);

        if (app == null) return NotFound();

        int nextSortOrder = app.Fields.Any() ? app.Fields.Max(f => f.SortOrder) + 1 : 0;

        var newField = new AppFieldDefinition
        {
            Id = Guid.NewGuid(),
            AppDefinitionId = appDefinitionId,
            Label = label,
            SystemName = systemName,
            DataType = dataType,
            IsRequired = isRequired,
            IsArray = isArray,
            SortOrder = nextSortOrder
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string entityCode, string icon)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(entityCode))
        {
            return BadRequest("Имя и системный код обязательны");
        }

        var newApp = new AppDefinition
        {
            Id = Guid.NewGuid(),
            Name = name,
            EntityCode = entityCode,
            Icon = icon ?? "box"
        };

        _context.AppDefinitions.Add(newApp);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}