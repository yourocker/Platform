using System.Text.Json;
using Core.Data;
using Core.Entities.Platform;
using Core.Entities.Platform.Form;
using Core.Interfaces;
using CRM.ViewModels.FormConfig;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Controllers.Api;

[Route("api/[controller]/[action]")]
[ApiController]
public class FormConfigController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ITransliterationService _transliterationService;

    public FormConfigController(AppDbContext context, ITransliterationService transliterationService)
    {
        _context = context;
        _transliterationService = transliterationService;
    }

    // GET: api/FormConfig/GetFields?appId=...&includeDeleted=true
    [HttpGet]
    public async Task<IActionResult> GetFields(Guid appId, bool includeDeleted = false)
    {
        var query = _context.AppFieldDefinitions
            .Where(f => f.AppDefinitionId == appId);

        if (!includeDeleted)
        {
            query = query.Where(f => !f.IsDeleted);
        }

        var fields = await query
            .OrderBy(f => f.IsDeleted)
            .ThenByDescending(f => f.IsSystem)
            .ThenBy(f => f.Label)
            .Select(f => new FieldDto
            {
                Id = f.Id,
                Label = f.Label,
                SystemName = f.SystemName,
                DataType = f.DataType.ToString(),
                IsRequired = f.IsRequired,
                IsArray = f.IsArray,
                IsSystem = f.IsSystem,
                IsDeleted = f.IsDeleted
            })
            .ToListAsync();

        return Ok(fields);
    }

    // POST: api/FormConfig/CreateField
    [HttpPost]
    public async Task<IActionResult> CreateField([FromBody] CreateFieldRequest request)
    {
        // 1. Используем нормальный сервис транслитерации
        var systemName = _transliterationService.TransliterateToSystemName(request.Label);
        
        // Защита от пустой строки, если Label состоял только из спецсимволов
        if (string.IsNullOrWhiteSpace(systemName))
        {
            systemName = $"Field_{Guid.NewGuid().ToString().Substring(0, 8)}";
        }

        // 2. Проверка уникальности
        var exists = await _context.AppFieldDefinitions
            .AnyAsync(f => f.AppDefinitionId == request.AppDefinitionId && f.SystemName == systemName);
            
        if (exists)
        {
            // Добавляем суффикс, если имя занято
            systemName += $"_{Guid.NewGuid().ToString().Substring(0, 4)}";
        }

        var field = new AppFieldDefinition
        {
            Id = Guid.NewGuid(),
            AppDefinitionId = request.AppDefinitionId,
            Label = request.Label,
            SystemName = systemName,
            DataType = request.DataType,
            IsArray = request.IsArray,
            IsRequired = request.IsRequired,
            IsSystem = false,
            IsDeleted = false
        };

        _context.AppFieldDefinitions.Add(field);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, fieldId = field.Id });
    }

    // POST: api/FormConfig/DeleteField?id=...
    [HttpPost]
    public async Task<IActionResult> DeleteField(Guid id)
    {
        var field = await _context.AppFieldDefinitions.FindAsync(id);
        if (field == null) return NotFound();

        if (field.IsSystem)
        {
            return BadRequest("Нельзя удалить системное поле.");
        }

        field.IsDeleted = true;
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    // POST: api/FormConfig/RestoreField?id=...
    [HttpPost]
    public async Task<IActionResult> RestoreField(Guid id)
    {
        var field = await _context.AppFieldDefinitions.FindAsync(id);
        if (field == null) return NotFound();

        field.IsDeleted = false;
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    // POST: api/FormConfig/SaveLayout
    [HttpPost]
    public async Task<IActionResult> SaveLayout([FromBody] SaveLayoutRequest request)
    {
        var form = await _context.AppFormDefinitions.FindAsync(request.FormId);
        if (form == null) return NotFound();

        // === ВАЛИДАЦИЯ JSON ===
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var layoutSchema = JsonSerializer.Deserialize<FormLayoutSchema>(request.LayoutJson, options);

            if (layoutSchema == null)
            {
                return BadRequest("Пустой или некорректный JSON макета.");
            }
            
            if (layoutSchema.Nodes == null)
            {
                 layoutSchema.Nodes = new List<LayoutNode>();
            }
            
            form.Layout = JsonSerializer.Serialize(layoutSchema, options);
        }
        catch (JsonException ex)
        {
            return BadRequest($"Ошибка валидации JSON: {ex.Message}");
        }

        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }
}