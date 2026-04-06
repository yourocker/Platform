using System.Collections.Generic;
using System.Linq;
using Core.Data;
using Core.Entities;
using Core.Entities.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System;
using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Rendering;
using Core.Entities.Tasks;
using CRM.Infrastructure;
using CRM.ViewModels.Filters;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace CRM.Controllers
{
    // Класс для жесткой типизации выборки имен
    public class LookupItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public abstract class BasePlatformController : Controller
    {
        protected readonly AppDbContext _context;
        protected readonly IWebHostEnvironment _hostingEnvironment;
        protected IFileStorageService FileStorageService => HttpContext.RequestServices.GetRequiredService<IFileStorageService>();

        protected BasePlatformController(AppDbContext context, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
        }

        protected async Task LoadDynamicFields(string systemCode)
        {
            var fields = await _context.AppFieldDefinitions
                .Where(f => f.AppDefinition.EntityCode == systemCode)
                .OrderBy(f => f.SortOrder)
                .ToListAsync();

            ViewBag.DynamicFields = fields;
            ViewBag.LookupData = await BuildEntityLinkLookupDataAsync(fields);
        }

        protected async Task<Dictionary<string, List<SelectListItem>>> BuildEntityLinkLookupDataAsync(IEnumerable<AppFieldDefinition> fields)
        {
            var lookupData = new Dictionary<string, List<SelectListItem>>();

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

        protected async Task<List<SelectListItem>> LoadEntityLinkLookupItemsAsync(string targetEntityCode)
        {
            var candidates = BuildEntityCodeCandidates(targetEntityCode);
            var normalized = NormalizeEntityCode(targetEntityCode);

            var items = await _context.GenericObjects
                .AsNoTracking()
                .Where(o => candidates.Contains(o.EntityCode))
                .OrderBy(o => o.Name)
                .Select(o => new SelectListItem
                {
                    Value = o.Id.ToString(),
                    Text = o.Name
                })
                .ToListAsync();

            if (items.Any())
            {
                return items;
            }

            if (normalized == "employee")
            {
                var employeeQuery = _context.Employees.AsNoTracking();
                if (_context.CurrentTenantId.HasValue)
                {
                    var currentTenantId = _context.CurrentTenantId.Value;
                    employeeQuery = employeeQuery.Where(e => e.TenantMemberships.Any(m =>
                        m.TenantId == currentTenantId &&
                        m.IsActive &&
                        !m.IsDismissed));
                }

                return await employeeQuery
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .Select(e => new SelectListItem
                    {
                        Value = e.Id.ToString(),
                        Text = e.FullName
                    })
                    .ToListAsync();
            }

            if (normalized == "department")
            {
                return await _context.Departments
                    .AsNoTracking()
                    .OrderBy(d => d.Name)
                    .Select(d => new SelectListItem
                    {
                        Value = d.Id.ToString(),
                        Text = d.Name
                    })
                    .ToListAsync();
            }

            if (normalized == "contact")
            {
                return await _context.Contacts
                    .AsNoTracking()
                    .OrderBy(c => c.FullName)
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.FullName
                    })
                    .ToListAsync();
            }

            return new List<SelectListItem>();
        }

        protected List<FilterFieldViewModel> BuildDynamicFilterFields(
            IEnumerable<AppFieldDefinition>? fields,
            IDictionary<string, List<SelectListItem>>? lookupData,
            IDictionary<string, string>? currentFilters,
            string keyPrefix = "f_dyn_")
        {
            var filterValues = currentFilters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var optionsLookup = lookupData ?? new Dictionary<string, List<SelectListItem>>(StringComparer.OrdinalIgnoreCase);

            return (fields ?? Enumerable.Empty<AppFieldDefinition>())
                .Where(field => !field.IsDeleted && field.DataType != FieldDataType.File)
                .OrderBy(field => field.SortOrder)
                .Select(field => new FilterFieldViewModel
                {
                    Key = $"{keyPrefix}{field.SystemName}",
                    Label = field.Label,
                    Kind = MapFilterInputKind(field.DataType),
                    Value = TryGetFilterValue(filterValues, $"{keyPrefix}{field.SystemName}"),
                    Options = BuildFilterOptions(field, optionsLookup)
                })
                .ToList();
        }

        protected static string? TryGetFilterValue(IDictionary<string, string>? values, string key)
        {
            return values != null && values.TryGetValue(key, out var value)
                ? value
                : null;
        }

        protected static FilterInputKind MapFilterInputKind(FieldDataType dataType)
        {
            return dataType switch
            {
                FieldDataType.Number or FieldDataType.Money => FilterInputKind.Number,
                FieldDataType.Date => FilterInputKind.Date,
                FieldDataType.DateTime => FilterInputKind.DateTime,
                FieldDataType.Boolean => FilterInputKind.Boolean,
                FieldDataType.Select => FilterInputKind.Select,
                FieldDataType.EntityLink => FilterInputKind.EntityLink,
                _ => FilterInputKind.Text
            };
        }

        protected static List<FilterOptionViewModel> BuildFilterOptions(
            AppFieldDefinition field,
            IDictionary<string, List<SelectListItem>> lookupData)
        {
            if (field.DataType == FieldDataType.Boolean)
            {
                return new List<FilterOptionViewModel>
                {
                    new() { Value = "true", Label = "Да" },
                    new() { Value = "false", Label = "Нет" }
                };
            }

            if ((field.DataType == FieldDataType.Select || field.DataType == FieldDataType.EntityLink) &&
                lookupData.TryGetValue(field.SystemName, out var items))
            {
                return items
                    .Select(item => new FilterOptionViewModel
                    {
                        Value = item.Value,
                        Label = item.Text
                    })
                    .ToList();
            }

            return new List<FilterOptionViewModel>();
        }

        protected static List<string> BuildEntityCodeCandidates(string? entityCode)
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

        protected static string NormalizeEntityCode(string? entityCode)
        {
            var normalized = (entityCode ?? string.Empty).Trim();
            if (normalized.EndsWith("s", StringComparison.OrdinalIgnoreCase) && normalized.Length > 1)
            {
                normalized = normalized[..^1];
            }

            return normalized.ToLowerInvariant();
        }

        protected async Task<HashSet<string>> BuildQuickCreatableEntityCodeSetAsync(IEnumerable<AppFieldDefinition> fields)
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
                .Where(d => candidates.Contains(d.EntityCode))
                .Select(d => d.EntityCode)
                .ToListAsync();

            var result = new HashSet<string>(definitionCodes, StringComparer.OrdinalIgnoreCase)
            {
                "Contact",
                "Contacts"
            };

            return result;
        }

        protected static bool CanQuickCreateEntity(string? targetEntityCode, ISet<string> quickCreatableEntityCodes)
        {
            if (string.IsNullOrWhiteSpace(targetEntityCode) || quickCreatableEntityCodes == null)
            {
                return false;
            }

            return BuildEntityCodeCandidates(targetEntityCode).Any(quickCreatableEntityCodes.Contains);
        }

        protected static string? BuildModalCreateUrl(string? targetEntityCode, ISet<string> quickCreatableEntityCodes)
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

        protected Guid? TryGetCurrentEmployeeId()
        {
            var employeeIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(employeeIdRaw, out var employeeId) ? employeeId : null;
        }

        protected async Task<Dictionary<string, string>> LoadFieldLabelMapAsync(string entityCode)
        {
            var fieldPairs = await _context.AppDefinitions
                .AsNoTracking()
                .Where(d => d.EntityCode == entityCode)
                .SelectMany(d => d.Fields.Where(f => !f.IsDeleted))
                .Select(f => new
                {
                    f.SystemName,
                    f.Label
                })
                .ToListAsync();

            return fieldPairs
                .Where(item => !string.IsNullOrWhiteSpace(item.SystemName))
                .GroupBy(item => item.SystemName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(item => item.Label)
                        .FirstOrDefault(label => !string.IsNullOrWhiteSpace(label))
                        ?? group.Key,
                    StringComparer.OrdinalIgnoreCase);
        }

        protected ContentResult BuildModalCreatedContentResult(string entityCode, Guid id, string? name)
        {
            return ModalRequestHelper.BuildEntityCreatedContent(entityCode, id, name);
        }

        protected ContentResult BuildModalUpdatedContentResult(string entityCode, Guid id, string? name)
        {
            return ModalRequestHelper.BuildEntityUpdatedContent(entityCode, id, name);
        }

        protected bool IsInlineSaveRequest()
        {
            var header = Request.Headers["X-CRM-Inline-Save"].ToString();
            return string.Equals(header, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(header, "1", StringComparison.OrdinalIgnoreCase);
        }

        protected JsonResult BuildInlineSaveSuccessResult(string entityCode, Guid id, string? name)
        {
            return Json(new
            {
                success = true,
                entityCode,
                id,
                name = name ?? string.Empty
            });
        }

        protected BadRequestObjectResult BuildInlineSaveValidationResult(string fallbackMessage = "Не удалось сохранить изменения. Проверьте заполнение полей.")
        {
            var errors = ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "Проверьте корректность введенных данных."
                    : error.ErrorMessage.Trim())
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Distinct()
                .ToList();

            return BadRequest(new
            {
                success = false,
                message = errors.FirstOrDefault() ?? fallbackMessage,
                errors
            });
        }

        protected void DeletePhysicalFile(string webPath)
        {
            if (string.IsNullOrEmpty(webPath)) return;
            try
            {
                if (FileStorageService.TryParseReference(webPath, out _))
                {
                    FileStorageService.DeleteByReferenceAsync(webPath).GetAwaiter().GetResult();
                    return;
                }

                var fullPath = Path.Combine(_hostingEnvironment.WebRootPath, webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch { }
        }

        [HttpPost("/files/delete-property")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFileProperty(Guid id, string entityCode, string propertyName, string filePath)
        {
            var entity = await FindDynamicEntityAsync(id, entityCode);
            if (entity == null || string.IsNullOrEmpty(entity.Properties)) return Json(new { success = false });

            var props = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Properties);
            if (props != null && props.ContainsKey(propertyName))
            {
                var val = props[propertyName];
                if (val is JsonElement el)
                {
                    if (el.ValueKind == JsonValueKind.Array)
                    {
                        var list = el.EnumerateArray().Select(x => x.GetString()).ToList();
                        if (list.Remove(filePath))
                        {
                            props[propertyName] = list.ToArray();
                            DeletePhysicalFile(filePath);
                        }
                    }
                    else if (el.GetString() == filePath)
                    {
                        props.Remove(propertyName);
                        DeletePhysicalFile(filePath);
                    }
                }

                entity.Properties = JsonSerializer.Serialize(props);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        protected async Task SaveDynamicProperties(IHasDynamicProperties entity, IFormCollection form, string entityCode)
        {
            var definitions = await _context.AppFieldDefinitions
                .Where(f => f.AppDefinition.EntityCode == entityCode)
                .ToListAsync();

            var resultDictionary = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(entity.Properties))
            {
                try 
                { 
                    resultDictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Properties) 
                                       ?? new Dictionary<string, object>(); 
                }
                catch { }
            }

            foreach (var def in definitions)
            {
                if (string.Equals(def.SystemName, "Name", StringComparison.OrdinalIgnoreCase))
                    continue;

                var formKey = $"DynamicProps[{def.SystemName}]";

                if (def.DataType == FieldDataType.File)
                {
                    var files = form.Files.GetFiles(formKey);

                    if (files.Any())
                    {
                        var newPaths = new List<string>();
                        var entityId = TryGetEntityId(entity);
                        foreach (var file in files)
                        {
                            if (file.Length > 20 * 1024 * 1024) 
                            {
                                ModelState.AddModelError(formKey, $"Файл '{file.FileName}' слишком велик.");
                                continue;
                            }

                            var storedFile = await FileStorageService.SaveAsync(
                                file,
                                "dynamic-fields",
                                entityCode,
                                entityId == Guid.Empty ? null : entityId);

                            newPaths.Add(FileStorageService.BuildAccessPath(storedFile.Id));
                        }

                        if (newPaths.Any())
                        {
                            if (def.IsArray)
                            {
                                var existingList = new List<string>();
                                if (resultDictionary.TryGetValue(def.SystemName, out var existingVal))
                                {
                                    if (existingVal is JsonElement el && el.ValueKind == JsonValueKind.Array)
                                        existingList = el.EnumerateArray().Select(x => x.GetString()).ToList();
                                    else if (existingVal is string[] arr)
                                        existingList = arr.ToList();
                                }
                                existingList.AddRange(newPaths);
                                resultDictionary[def.SystemName] = existingList.ToArray();
                            }
                            else
                            {
                                if (resultDictionary.TryGetValue(def.SystemName, out var oldPathVal))
                                {
                                    var oldPath = oldPathVal is JsonElement el ? el.GetString() : oldPathVal?.ToString();
                                    DeletePhysicalFile(oldPath);
                                }
                                resultDictionary[def.SystemName] = newPaths.First();
                            }
                        }
                    }
                    continue;
                }

                if (form.ContainsKey(formKey))
                {
                    var formValues = form[formKey];
                    var rawValues = formValues.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

                    if (!rawValues.Any()) 
                    {
                        if (def.IsRequired)
                            ModelState.AddModelError(formKey, $"Поле '{def.Label}' обязательно.");
                        else
                            resultDictionary.Remove(def.SystemName);

                        continue;
                    }

                    try
                    {
                        if (def.IsArray)
                        {
                            var parsedList = new List<object>();
                            foreach (var val in rawValues) parsedList.Add(ParseAndValidateValue(val, def, formKey));
                            resultDictionary[def.SystemName] = parsedList.ToArray();
                        }
                        else
                        {
                            resultDictionary[def.SystemName] = ParseAndValidateValue(rawValues.First(), def, formKey);
                        }
                    }
                    catch (FormatException) { continue; }
                }
                else if (def.IsRequired && !resultDictionary.ContainsKey(def.SystemName))
                {
                    ModelState.AddModelError(formKey, $"Поле '{def.Label}' обязательно.");
                }
            }

            var options = new JsonSerializerOptions 
            { 
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };
            
            entity.Properties = JsonSerializer.Serialize(resultDictionary, options);
        }

        protected void FinalizeDynamicFilePaths(IHasDynamicProperties entity, string entityCode, string recordId)
        {
            if (string.IsNullOrEmpty(entity.Properties)) return;

            var props = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.Properties);
            if (props == null) return;

            bool hasChanges = false;
            var finalBaseFolder = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", entityCode, recordId);

            var keys = props.Keys.ToList();
            foreach (var key in keys)
            {
                var value = props[key];
                if (value is JsonElement jEl)
                {
                    if (jEl.ValueKind == JsonValueKind.String)
                    {
                        var path = jEl.GetString();
                        if (!string.IsNullOrEmpty(path) && path.Contains("/uploads/temp/"))
                        {
                            var newPath = MoveFileToFinal(path, finalBaseFolder);
                            if (newPath != null) { props[key] = newPath; hasChanges = true; }
                        }
                    }
                    else if (jEl.ValueKind == JsonValueKind.Array)
                    {
                        var newItems = new List<object>();
                        bool arrayChanged = false;
                        foreach (var item in jEl.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                var path = item.GetString();
                                if (!string.IsNullOrEmpty(path) && path.Contains("/uploads/temp/"))
                                {
                                    var newPath = MoveFileToFinal(path, finalBaseFolder);
                                    if (newPath != null) { newItems.Add(newPath); arrayChanged = true; hasChanges = true; }
                                    else newItems.Add(path);
                                }
                                else newItems.Add(path);
                            }
                            else newItems.Add(item);
                        }
                        if (arrayChanged) props[key] = newItems.ToArray();
                    }
                }
            }

            if (hasChanges)
            {
                entity.Properties = JsonSerializer.Serialize(props, new JsonSerializerOptions { 
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = true
                });
            }
        }

        private Guid TryGetEntityId(IHasDynamicProperties entity)
        {
            var idValue = entity.GetType().GetProperty("Id")?.GetValue(entity);
            return idValue is Guid guid ? guid : Guid.Empty;
        }

        private async Task<IHasDynamicProperties?> FindDynamicEntityAsync(Guid id, string entityCode)
        {
            var normalizedCode = NormalizeEntityCode(entityCode);
            return normalizedCode switch
            {
                "employee" => await _context.Employees.FirstOrDefaultAsync(x => x.Id == id),
                "position" => await _context.Positions.FirstOrDefaultAsync(x => x.Id == id),
                "department" => await _context.Departments.FirstOrDefaultAsync(x => x.Id == id),
                "resourcebooking" => await _context.CrmResourceBookings.FirstOrDefaultAsync(x => x.Id == id),
                _ => await _context.GenericObjects.FirstOrDefaultAsync(x => x.Id == id)
            };
        }

        private string MoveFileToFinal(string tempWebPath, string finalBaseDir)
        {
            try
            {
                var tempSystemPath = _hostingEnvironment.WebRootPath + tempWebPath.Replace("/", Path.DirectorySeparatorChar.ToString());
                if (!System.IO.File.Exists(tempSystemPath)) return null;
                if (!Directory.Exists(finalBaseDir)) Directory.CreateDirectory(finalBaseDir);

                var fileName = Path.GetFileName(tempSystemPath);
                var finalSystemPath = Path.Combine(finalBaseDir, fileName);
                if (System.IO.File.Exists(finalSystemPath))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    var newName = $"{nameWithoutExt}_{Guid.NewGuid().ToString().Substring(0, 4)}{ext}";
                    finalSystemPath = Path.Combine(finalBaseDir, newName);
                    fileName = newName;
                }

                System.IO.File.Move(tempSystemPath, finalSystemPath);
                var tempDir = Path.GetDirectoryName(tempSystemPath);
                if (Directory.Exists(tempDir) && !Directory.EnumerateFileSystemEntries(tempDir).Any()) Directory.Delete(tempDir);

                var relativeDir = finalBaseDir.Replace(_hostingEnvironment.WebRootPath, "").Replace("\\", "/");
                if (!relativeDir.StartsWith("/")) relativeDir = "/" + relativeDir;
                return $"{relativeDir.TrimEnd('/')}/{fileName}";
            }
            catch { return null; }
        }

        private object ParseAndValidateValue(string val, AppFieldDefinition def, string formKey)
        {
            switch (def.DataType)
            {
                case FieldDataType.Number:
                case FieldDataType.Money:
                    if (decimal.TryParse(val.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d)) return d;
                    ModelState.AddModelError(formKey, $"Значение '{val}' в поле '{def.Label}' не число.");
                    throw new FormatException();
                case FieldDataType.Date:
                case FieldDataType.DateTime:
                    if (DateTime.TryParse(val, out DateTime dt)) return dt;
                    ModelState.AddModelError(formKey, $"Значение '{val}' в поле '{def.Label}' не дата.");
                    throw new FormatException();
                case FieldDataType.Boolean:
                    return val.Contains("true", StringComparison.OrdinalIgnoreCase);
                case FieldDataType.EntityLink:
                    return val;
                case FieldDataType.Select:
                    var availableValues = def.GetSelectOptions()
                        .Select(option => option.Value)
                        .ToHashSet(StringComparer.Ordinal);
                    if (availableValues.Contains(val))
                    {
                        return val;
                    }

                    ModelState.AddModelError(formKey, $"Значение '{val}' отсутствует в списке поля '{def.Label}'.");
                    throw new FormatException();
                default:
                    return val;
            }
        }
        protected async Task ResolveRelatedEntityNames(List<EmployeeTask> tasks)
        {
            // Собираем все связи из всех задач в один плоский список
            var allRelations = tasks.SelectMany(t => t.Relations).ToList();
            if (!allRelations.Any()) return;

            // Группируем по коду сущности (Patient, Employee и т.д.), чтобы дергать базу один раз для каждого типа
            var groupedRelations = allRelations.GroupBy(r => r.EntityCode);

            foreach (var group in groupedRelations)
            {
                var entityCode = group.Key;
                var entityIds = group.Select(r => r.EntityId).Distinct().ToList();

                // Динамически получаем таблицу через DbContext
                var dbSet = _context.GetType().GetProperties()
                    .FirstOrDefault(p => p.Name.Contains(entityCode + "s"))?.GetValue(_context) as IQueryable<object>;

                if (dbSet != null)
                {
                    // Вытягиваем ID и Имя (FullName или Name)
                    var namesMap = await dbSet
                        .Where(e => entityIds.Contains((Guid)e.GetType().GetProperty("Id").GetValue(e)))
                        .ToListAsync();

                    foreach (var relation in group)
                    {
                        var entity = namesMap.FirstOrDefault(e => (Guid)e.GetType().GetProperty("Id").GetValue(e) == relation.EntityId);
                        if (entity != null)
                        {
                            // Пробуем взять FullName, если нет — Name
                            relation.EntityName = entity.GetType().GetProperty("FullName")?.GetValue(entity)?.ToString() 
                                                  ?? entity.GetType().GetProperty("Name")?.GetValue(entity)?.ToString();
                        }
                    }
                }
            }
        }
    }
}
