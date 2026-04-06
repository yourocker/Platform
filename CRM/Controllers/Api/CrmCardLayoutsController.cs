using Core.Data;
using Core.Entities.CRM;
using Core.Entities.Platform;
using CRM.ViewModels.CRM;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CRM.Controllers.Api;

[Route("api/[controller]/[action]")]
[ApiController]
public class CrmCardLayoutsController : ControllerBase
{
    private readonly AppDbContext _context;

    public CrmCardLayoutsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetState(Guid pipelineId)
    {
        if (pipelineId == Guid.Empty)
        {
            return BadRequest(new { success = false, message = "Не передана воронка." });
        }

        var pipeline = await _context.CrmPipelines
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == pipelineId && x.IsActive);

        if (pipeline == null)
        {
            return NotFound(new { success = false, message = "Воронка не найдена." });
        }

        var appDefinition = await _context.AppDefinitions
            .AsNoTracking()
            .Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.EntityCode == pipeline.TargetEntityCode);

        if (appDefinition == null)
        {
            return NotFound(new { success = false, message = "Не найдена конфигурация сущности." });
        }

        var fields = appDefinition.Fields
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Label)
            .ToList();

        var existingLayout = await _context.CrmPipelineCardLayouts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PipelineId == pipelineId);

        var layout = CrmCardLayoutCatalog.ParseOrDefault(existingLayout?.Layout, pipeline.TargetEntityCode, fields);
        var lookupData = await BuildLookupDataAsync(fields);
        var quickCreatableEntityCodes = await BuildQuickCreatableEntityCodeSetAsync(fields);
        var dynamicFieldCreateUrls = fields
            .Where(field => field.DataType == FieldDataType.EntityLink)
            .Where(field => !string.IsNullOrWhiteSpace(field.TargetEntityCode))
            .ToDictionary(
                field => field.SystemName,
                field => BuildModalCreateUrl(field.TargetEntityCode, quickCreatableEntityCodes) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        return Ok(new
        {
            success = true,
            pipelineId = pipeline.Id,
            entityCode = pipeline.TargetEntityCode,
            layout,
            layoutJson = System.Text.Json.JsonSerializer.Serialize(layout),
            palette = CrmCardLayoutCatalog.BuildPalette(pipeline.TargetEntityCode, fields),
            fields = fields.Select(field => new
            {
                id = field.Id,
                label = field.Label,
                systemName = field.SystemName,
                dataType = field.DataType.ToString(),
                isRequired = field.IsRequired,
                isArray = field.IsArray,
                targetEntityCode = field.TargetEntityCode,
                sortOrder = field.SortOrder,
                selectOptions = field.GetSelectOptions()
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new
                    {
                        value = option.Value,
                        label = option.Label
                    })
                    .ToList()
            }),
            lookupData = lookupData.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Select(item => new
                {
                    value = item.Value,
                    text = item.Text
                }).ToList(),
                StringComparer.OrdinalIgnoreCase),
            dynamicFieldCreateUrls
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] CrmCardLayoutSaveRequest request)
    {
        if (request.PipelineId == Guid.Empty)
        {
            return BadRequest("Не передана воронка.");
        }

        var pipeline = await _context.CrmPipelines
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PipelineId && x.IsActive);

        if (pipeline == null)
        {
            return NotFound();
        }

        var appDefinition = await _context.AppDefinitions
            .AsNoTracking()
            .Include(x => x.Fields)
            .FirstOrDefaultAsync(x => x.EntityCode == pipeline.TargetEntityCode);

        if (appDefinition == null)
        {
            return NotFound();
        }

        var fields = appDefinition.Fields
            .Where(x => !x.IsDeleted)
            .ToList();

        var schema = CrmCardLayoutCatalog.ParseOrDefault(request.LayoutJson, pipeline.TargetEntityCode, fields);
        var normalizedJson = System.Text.Json.JsonSerializer.Serialize(schema);

        var existing = await _context.CrmPipelineCardLayouts
            .FirstOrDefaultAsync(x => x.PipelineId == request.PipelineId);

        if (existing == null)
        {
            existing = new CrmPipelineCardLayout
            {
                Id = Guid.NewGuid(),
                PipelineId = request.PipelineId,
                Layout = normalizedJson,
                UpdatedAt = DateTime.UtcNow
            };
            _context.CrmPipelineCardLayouts.Add(existing);
        }
        else
        {
            existing.Layout = normalizedJson;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            layout = schema,
            layoutJson = normalizedJson
        });
    }

    private async Task<Dictionary<string, List<SelectListItem>>> BuildLookupDataAsync(IEnumerable<AppFieldDefinition> fields)
    {
        var lookupData = new Dictionary<string, List<SelectListItem>>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in (fields ?? Enumerable.Empty<AppFieldDefinition>())
                     .Where(f => f.DataType == FieldDataType.EntityLink && !string.IsNullOrWhiteSpace(f.TargetEntityCode)))
        {
            lookupData[field.SystemName] = await LoadEntityLinkLookupItemsAsync(field.TargetEntityCode!);
        }

        foreach (var field in (fields ?? Enumerable.Empty<AppFieldDefinition>())
                     .Where(f => f.DataType == FieldDataType.Select))
        {
            lookupData[field.SystemName] = field.GetSelectOptions()
                .OrderBy(option => option.SortOrder)
                .Select(option => new SelectListItem
                {
                    Value = option.Value,
                    Text = option.Label
                })
                .ToList();
        }

        return lookupData;
    }

    private async Task<List<SelectListItem>> LoadEntityLinkLookupItemsAsync(string targetEntityCode)
    {
        var candidates = BuildEntityCodeCandidates(targetEntityCode);
        var normalized = NormalizeEntityCode(targetEntityCode);

        var genericItems = await _context.GenericObjects
            .AsNoTracking()
            .Where(objectItem => candidates.Contains(objectItem.EntityCode))
            .OrderBy(objectItem => objectItem.Name)
            .Select(objectItem => new SelectListItem
            {
                Value = objectItem.Id.ToString(),
                Text = objectItem.Name
            })
            .ToListAsync();

        if (genericItems.Any())
        {
            return genericItems;
        }

        if (normalized == "employee")
        {
            var employeeQuery = _context.Employees.AsNoTracking();
            if (_context.CurrentTenantId.HasValue)
            {
                var currentTenantId = _context.CurrentTenantId.Value;
                employeeQuery = employeeQuery.Where(employee => employee.TenantMemberships.Any(membership =>
                    membership.TenantId == currentTenantId &&
                    membership.IsActive &&
                    !membership.IsDismissed));
            }

            return await employeeQuery
                .OrderBy(employee => employee.LastName)
                .ThenBy(employee => employee.FirstName)
                .Select(employee => new SelectListItem
                {
                    Value = employee.Id.ToString(),
                    Text = employee.FullName
                })
                .ToListAsync();
        }

        if (normalized == "department")
        {
            return await _context.Departments
                .AsNoTracking()
                .OrderBy(department => department.Name)
                .Select(department => new SelectListItem
                {
                    Value = department.Id.ToString(),
                    Text = department.Name
                })
                .ToListAsync();
        }

        if (normalized == "contact")
        {
            return await _context.Contacts
                .AsNoTracking()
                .OrderBy(contact => contact.FullName)
                .Select(contact => new SelectListItem
                {
                    Value = contact.Id.ToString(),
                    Text = contact.FullName
                })
                .ToListAsync();
        }

        return new List<SelectListItem>();
    }

    private async Task<HashSet<string>> BuildQuickCreatableEntityCodeSetAsync(IEnumerable<AppFieldDefinition> fields)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in (fields ?? Enumerable.Empty<AppFieldDefinition>())
                     .Where(f => f.DataType == FieldDataType.EntityLink && !string.IsNullOrWhiteSpace(f.TargetEntityCode)))
        {
            foreach (var candidate in BuildEntityCodeCandidates(field.TargetEntityCode))
            {
                candidates.Add(candidate);
            }
        }

        var definitionCodes = await _context.AppDefinitions
            .AsNoTracking()
            .Where(definition => candidates.Contains(definition.EntityCode))
            .Select(definition => definition.EntityCode)
            .ToListAsync();

        var result = new HashSet<string>(definitionCodes, StringComparer.OrdinalIgnoreCase)
        {
            "Contact",
            "Contacts"
        };

        return result;
    }

    private static List<string> BuildEntityCodeCandidates(string? entityCode)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalized = (entityCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return candidates.ToList();
        }

        candidates.Add(normalized);

        if (normalized.EndsWith("s", StringComparison.OrdinalIgnoreCase) && normalized.Length > 1)
        {
            candidates.Add(normalized[..^1]);
        }
        else
        {
            candidates.Add(normalized + "s");
        }

        return candidates.ToList();
    }

    private static string NormalizeEntityCode(string? entityCode)
    {
        var normalized = (entityCode ?? string.Empty).Trim();
        if (normalized.EndsWith("s", StringComparison.OrdinalIgnoreCase) && normalized.Length > 1)
        {
            normalized = normalized[..^1];
        }

        return normalized.ToLowerInvariant();
    }

    private static string? BuildModalCreateUrl(string? targetEntityCode, ISet<string> quickCreatableEntityCodes)
    {
        if (string.IsNullOrWhiteSpace(targetEntityCode) || quickCreatableEntityCodes == null)
        {
            return null;
        }

        var normalized = NormalizeEntityCode(targetEntityCode);
        if (normalized == "contact")
        {
            return "/Contacts/Create?modal=true";
        }

        var resolvedCode = quickCreatableEntityCodes
            .FirstOrDefault(code => string.Equals(NormalizeEntityCode(code), normalized, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(resolvedCode))
        {
            return null;
        }

        return $"/Data/{resolvedCode}/Create?modal=true";
    }
}
