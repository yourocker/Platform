using System.Collections.Generic;
using System.Linq;
using MedicalBot.Data;
using MedicalBot.Entities;
using MedicalBot.Entities.Platform;
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
using MedicalBot.Entities.Tasks;

namespace MedicalWeb.Controllers
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

            // Подготовка данных для полей типа EntityLink (Связь с объектом)
            var lookupData = new Dictionary<string, List<SelectListItem>>();
            foreach (var field in fields.Where(f => f.DataType == FieldDataType.EntityLink))
            {
                if (!string.IsNullOrEmpty(field.TargetEntityCode))
                {
                    // Получаем записи целевой сущности, используя физическое свойство Name для заголовка
                    var data = await _context.GenericObjects
                        .Where(o => o.EntityCode == field.TargetEntityCode)
                        .OrderBy(o => o.Name) // Сортируем по имени для удобства выбора
                        .Select(o => new SelectListItem
                        {
                            Value = o.Id.ToString(),
                            Text = o.Name // Используем поле Name из вашей модели GenericObject
                        })
                        .ToListAsync();
                    
                    lookupData[field.SystemName] = data;
                }
            }
            ViewBag.LookupData = lookupData;
        }

        protected void DeletePhysicalFile(string webPath)
        {
            if (string.IsNullOrEmpty(webPath)) return;
            try
            {
                var fullPath = Path.Combine(_hostingEnvironment.WebRootPath, webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(fullPath)) 
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch { }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFileProperty(Guid id, string entityCode, string propertyName, string filePath)
        {
            var entity = await _context.GenericObjects.FindAsync(id);
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
                var formKey = $"DynamicProps[{def.SystemName}]";

                if (def.DataType == FieldDataType.File)
                {
                    var files = form.Files.GetFiles(formKey);

                    if (files.Any())
                    {
                        var newPaths = new List<string>();
                        foreach (var file in files)
                        {
                            if (file.Length > 20 * 1024 * 1024) 
                            {
                                ModelState.AddModelError(formKey, $"Файл '{file.FileName}' слишком велик.");
                                continue;
                            }

                            var folderGuid = Guid.NewGuid().ToString();
                            var uploadDir = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", "temp", folderGuid);
                            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                            var safeFileName = Path.GetFileName(file.FileName); 
                            var filePath = Path.Combine(uploadDir, safeFileName);
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }
                            newPaths.Add($"/uploads/temp/{folderGuid}/{safeFileName}");
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