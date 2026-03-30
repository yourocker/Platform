using System.Security.Claims;
using System.Text.Json;
using Core.Data;
using Core.Entities.System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Controllers;

[Route("FilterPresets")]
public class FilterPresetsController(AppDbContext context) : Controller
{
    private readonly AppDbContext _context = context;

    [HttpGet("Get")]
    public async Task<IActionResult> Get(string entityCode, string viewCode = "Index")
    {
        var userId = TryGetCurrentEmployeeId();
        if (userId == null || string.IsNullOrWhiteSpace(entityCode))
        {
            return Json(Array.Empty<object>());
        }

        var presets = await _context.UserFilterPresets
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.EntityCode == entityCode && x.ViewCode == viewCode)
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Json(presets.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            values = DeserializeFilterValues(p.FiltersJson)
        }));
    }

    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(IFormCollection form)
    {
        var userId = TryGetCurrentEmployeeId();
        var entityCode = form["entityCode"].ToString().Trim();
        var viewCode = string.IsNullOrWhiteSpace(form["viewCode"])
            ? "Index"
            : form["viewCode"].ToString().Trim();
        var name = form["name"].ToString().Trim();

        if (userId == null)
        {
            return Json(new { success = false, message = "Не удалось определить пользователя." });
        }

        if (string.IsNullOrWhiteSpace(entityCode) || string.IsNullOrWhiteSpace(name))
        {
            return Json(new { success = false, message = "Не заполнены обязательные параметры пресета." });
        }

        var values = ReadValuesFromForm(form);
        var serialized = JsonSerializer.Serialize(values);

        var preset = await _context.UserFilterPresets
            .FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.EntityCode == entityCode &&
                x.ViewCode == viewCode &&
                x.Name == name);

        if (preset == null)
        {
            preset = new UserFilterPreset
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                EntityCode = entityCode,
                ViewCode = viewCode,
                Name = name,
                FiltersJson = serialized,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserFilterPresets.Add(preset);
        }
        else
        {
            preset.FiltersJson = serialized;
            preset.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Json(new { success = true, id = preset.Id });
    }

    [HttpPost("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = TryGetCurrentEmployeeId();
        if (userId == null)
        {
            return Json(new { success = false, message = "Не удалось определить пользователя." });
        }

        var preset = await _context.UserFilterPresets
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (preset == null)
        {
            return Json(new { success = false, message = "Пресет не найден." });
        }

        _context.UserFilterPresets.Remove(preset);
        await _context.SaveChangesAsync();

        return Json(new { success = true });
    }

    private Guid? TryGetCurrentEmployeeId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }

    private static Dictionary<string, string> ReadValuesFromForm(IFormCollection form)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in form.Keys.Where(key => key.StartsWith("values[", StringComparison.OrdinalIgnoreCase) && key.EndsWith("]")))
        {
            var logicalKey = key["values[".Length..^1];
            values[logicalKey] = form[key].ToString();
        }

        return values;
    }

    private static Dictionary<string, string> DeserializeFilterValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ??
                   new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
