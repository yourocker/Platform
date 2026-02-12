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

    #region Управление полями (AppFieldDefinition)

    [HttpGet]
    public async Task<IActionResult> GetFields(Guid appId, bool includeDeleted = false)
    {
        var query = _context.AppFieldDefinitions.Where(f => f.AppDefinitionId == appId);
        if (!includeDeleted) query = query.Where(f => !f.IsDeleted);

        var fields = await query
            .OrderBy(f => f.IsDeleted)
            .ThenBy(f => f.SortOrder)
            .Select(f => new FieldDto
            {
                Id = f.Id,
                Label = f.Label,
                SystemName = f.SystemName,
                DataType = f.DataType.ToString(),
                IsRequired = f.IsRequired,
                IsArray = f.IsArray,
                IsSystem = f.IsSystem,
                IsDeleted = f.IsDeleted,
                Description = f.Description,
                TargetEntityCode = f.TargetEntityCode,
                SortOrder = f.SortOrder
            }).ToListAsync();

        return Ok(fields);
    }

    [HttpPost]
    public async Task<IActionResult> CreateField([FromBody] CreateFieldRequest request)
    {
        if (await _context.AppFieldDefinitions.AnyAsync(f => f.AppDefinitionId == request.AppDefinitionId && f.Label == request.Label))
            return BadRequest($"Поле с названием '{request.Label}' уже существует.");

        string systemName = string.IsNullOrWhiteSpace(request.SystemName) 
            ? "UF_" + _transliterationService.TransliterateToSystemName(request.Label) 
            : request.SystemName;

        if (await _context.AppFieldDefinitions.AnyAsync(f => f.AppDefinitionId == request.AppDefinitionId && f.SystemName == systemName))
            return BadRequest($"Системное имя '{systemName}' уже занято.");

        var field = new AppFieldDefinition
        {
            Id = Guid.NewGuid(),
            AppDefinitionId = request.AppDefinitionId,
            Label = request.Label,
            SystemName = systemName,
            DataType = request.DataType,
            IsRequired = request.IsRequired,
            IsArray = request.IsArray,
            Description = request.Description,
            TargetEntityCode = request.TargetEntityCode,
            SortOrder = (_context.AppFieldDefinitions.Where(f => f.AppDefinitionId == request.AppDefinitionId).Max(f => (int?)f.SortOrder) ?? 0) + 1
        };

        _context.AppFieldDefinitions.Add(field);
        await _context.SaveChangesAsync();
        return Ok(new { success = true, id = field.Id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteField(Guid id)
    {
        var field = await _context.AppFieldDefinitions.FindAsync(id);
        if (field == null) return NotFound();
        if (field.IsSystem) return BadRequest("Запрещено удалять системные поля.");

        field.IsDeleted = true;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> RestoreField(Guid id)
    {
        var field = await _context.AppFieldDefinitions.FindAsync(id);
        if (field == null) return NotFound();

        field.IsDeleted = false;
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    #endregion

    #region Управление макетами (AppFormDefinition)

    [HttpGet]
    public async Task<IActionResult> GetForms(Guid appId)
    {
        var forms = await _context.AppFormDefinitions
            .Where(f => f.AppDefinitionId == appId)
            .OrderBy(f => f.Type).ThenBy(f => f.Name)
            .Select(f => new FormDefinitionDto { Id = f.Id, Name = f.Name, Type = f.Type, IsDefault = f.IsDefault })
            .ToListAsync();
        return Ok(forms);
    }

    [HttpPost]
    public async Task<IActionResult> CreateForm([FromBody] CreateFormRequest request)
    {
        bool isFirst = !await _context.AppFormDefinitions.AnyAsync(f => f.AppDefinitionId == request.AppDefinitionId && f.Type == request.Type);
        
        var form = new AppFormDefinition
        {
            Id = Guid.NewGuid(),
            AppDefinitionId = request.AppDefinitionId,
            Name = request.Name,
            Type = request.Type,
            IsDefault = isFirst,
            Layout = "{ \"Nodes\": [] }"
        };

        _context.AppFormDefinitions.Add(form);
        await _context.SaveChangesAsync();
        return Ok(new { success = true, id = form.Id });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteForm(Guid id)
    {
        var form = await _context.AppFormDefinitions.FindAsync(id);
        if (form == null) return NotFound();
        if (form.IsDefault) return BadRequest("Нельзя удалить форму по умолчанию.");

        _context.AppFormDefinitions.Remove(form);
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> SaveLayout([FromBody] SaveLayoutRequest request)
    {
        var form = await _context.AppFormDefinitions.Include(f => f.AppDefinition).ThenInclude(a => a.Fields)
            .FirstOrDefaultAsync(f => f.Id == request.FormId);
        if (form == null) return NotFound();

        try 
        {
            var layoutSchema = JsonSerializer.Deserialize<FormLayoutSchema>(request.LayoutJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (layoutSchema == null) return BadRequest("Некорректный JSON.");

            // Валидация на пропущенные обязательные поля
            if (!request.ForceSave)
            {
                var requiredFieldIds = form.AppDefinition.Fields.Where(f => f.IsRequired && !f.IsDeleted).Select(f => f.Id).ToList();
                var layoutFieldIds = ExtractFieldIds(layoutSchema.Nodes);
                var missingIds = requiredFieldIds.Except(layoutFieldIds).ToList();

                if (missingIds.Any())
                {
                    var names = string.Join(", ", form.AppDefinition.Fields.Where(f => missingIds.Contains(f.Id)).Select(f => f.Label));
                    return Ok(new { success = false, warning = true, message = $"На форме отсутствуют обязательные поля: {names}" });
                }
            }

            form.Layout = request.LayoutJson;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }
        catch (JsonException ex) { return BadRequest($"Ошибка JSON: {ex.Message}"); }
    }

    private List<Guid> ExtractFieldIds(List<LayoutNode> nodes)
    {
        var ids = new List<Guid>();
        foreach (var node in nodes)
        {
            if (node is FieldNode fn) ids.Add(fn.FieldId);
            else if (node is TabControlNode tcn) foreach (var tab in tcn.Tabs) ids.AddRange(ExtractFieldIds(tab.Children));
            else if (node is TabNode tn) ids.AddRange(ExtractFieldIds(tn.Children));
            else if (node is GroupNode gn) ids.AddRange(ExtractFieldIds(gn.Children));
            else if (node is RowNode rn) foreach (var col in rn.Columns) ids.AddRange(ExtractFieldIds(col.Children));
            else if (node is ColumnNode cn) ids.AddRange(ExtractFieldIds(cn.Children));
        }
        return ids;
    }

    #endregion
}