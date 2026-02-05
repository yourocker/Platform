using Core.Data;
using Core.Entities.Platform;
using Core.FormEngine.Domain;
using Core.FormEngine.Schema;
using CRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Core.Entities.CRM;

namespace CRM.Controllers
{
    public class FormDesignerController : Controller
    {
        private readonly AppDbContext _context;

        public FormDesignerController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(Guid entityId, Guid? formId = null)
        {
            var entityDef = await _context.AppDefinitions
                .Include(x => x.Fields)
                .FirstOrDefaultAsync(x => x.Id == entityId);

            if (entityDef == null) return NotFound();

            // 1. Палитра полей (Системные + Динамические)
            var designerFields = BuildDesignerFields(entityDef);

            // 2. Список существующих форм
            var existingForms = await _context.AppFormDefinitions
                .Where(x => x.AppDefinitionId == entityId)
                .OrderBy(x => x.Type).ThenBy(x => x.Name)
                .ToListAsync();

            AppFormDefinition? currentForm = null;
            string? layoutJson = null;

            if (formId.HasValue)
            {
                currentForm = existingForms.FirstOrDefault(x => x.Id == formId.Value);
                if (currentForm != null && currentForm.Layout != null)
                {
                    // ВАЖНО: Сериализуем Layout обратно в строку для JS
                    // Используем CamelCase, чтобы в JS было { type: "group" }, а не { Type: "group" }
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    layoutJson = JsonSerializer.Serialize(currentForm.Layout, options);
                }
            }

            var viewModel = new FormDesignerViewModel
            {
                EntityId = entityDef.Id,
                EntityName = entityDef.Name,
                AvailableFields = designerFields,
                ExistingForms = existingForms,
                CurrentForm = currentForm,
                LayoutJson = layoutJson // Вот здесь данные пойдут на фронт
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> SaveConfig([FromBody] SaveFormRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.LayoutJson))
                return BadRequest("Пустой запрос");

            FormLayoutSchema? schema;
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                schema = JsonSerializer.Deserialize<FormLayoutSchema>(request.LayoutJson, options);
            }
            catch (Exception ex)
            {
                return BadRequest($"JSON Error: {ex.Message}");
            }

            if (schema == null) return BadRequest("Invalid Schema");

            AppFormDefinition? formDef;

            if (request.FormId.HasValue)
            {
                // UPDATE
                formDef = await _context.AppFormDefinitions.FirstOrDefaultAsync(x => x.Id == request.FormId.Value);
                if (formDef == null) return NotFound("Form not found");

                formDef.Layout = schema;
                formDef.UpdatedAt = DateTime.UtcNow;
                
                // Если передали имя и тип - обновляем
                if (!string.IsNullOrEmpty(request.Title)) formDef.Name = request.Title;
                if (request.Type.HasValue) formDef.Type = request.Type.Value;

                _context.Update(formDef);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Форма обновлена" });
            }
            else
            {
                // CREATE
                if (string.IsNullOrEmpty(request.Title)) return BadRequest("Укажите название формы");

                formDef = new AppFormDefinition
                {
                    Id = Guid.NewGuid(),
                    AppDefinitionId = request.EntityId,
                    Name = request.Title,
                    Type = request.Type ?? FormType.Edit, // Дефолт
                    FormCode = $"form_{Guid.NewGuid():N}[0..8]",
                    Layout = schema,
                    UpdatedAt = DateTime.UtcNow,
                    IsDefault = false
                };

                _context.AppFormDefinitions.Add(formDef);
                await _context.SaveChangesAsync();

                var redirectUrl = Url.Action("Index", new { entityId = request.EntityId, formId = formDef.Id });
                return Ok(new { message = "Форма создана", redirectUrl });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            var form = await _context.AppFormDefinitions.FindAsync(id);
            if (form == null) return NotFound();

            var entityId = form.AppDefinitionId;
            _context.AppFormDefinitions.Remove(form);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { entityId });
        }

        // --- Хелперы ---

        private List<DesignerFieldDto> BuildDesignerFields(AppDefinition entityDef)
        {
            var fields = entityDef.Fields.Select(f => new DesignerFieldDto
            {
                Name = f.SystemName,
                Label = f.Label,
                DataType = f.DataType.ToString(),
                IsSystem = false,
                IsCollection = f.IsArray
            }).ToList();

            if (entityDef.IsSystem && !string.IsNullOrEmpty(entityDef.EntityCode))
            {
                var sysFields = GetSystemProperties(entityDef.EntityCode);
                fields.InsertRange(0, sysFields);
            }
            return fields;
        }

        private List<DesignerFieldDto> GetSystemProperties(string entityCode)
        {
            Type? type = entityCode switch
            {
                "Contact" => typeof(Contact),
                _ => null
            };

            if (type == null) return new List<DesignerFieldDto>();

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "Id" && p.Name != "TenantId" && p.Name != "IsDeleted"
                            && p.Name != "CreatedAt" && p.Name != "UpdatedAt")
                .ToList();

            var result = new List<DesignerFieldDto>();

            foreach (var p in props)
            {
                bool isCollection = typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)
                                    && p.PropertyType != typeof(string);

                if (!isCollection && p.PropertyType.IsClass && p.PropertyType != typeof(string))
                    continue;

                var displayAttr = p.GetCustomAttribute<DisplayAttribute>();
                string label = displayAttr?.Name ?? p.Name;

                string dataType = "String";
                if (p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?)) dataType = "DateTime";
                else if (p.PropertyType == typeof(bool) || p.PropertyType == typeof(bool?)) dataType = "Boolean";
                else if (p.PropertyType == typeof(int) || p.PropertyType == typeof(decimal)) dataType = "Number";
                else if (isCollection) dataType = "Collection";

                result.Add(new DesignerFieldDto
                {
                    Name = p.Name,
                    Label = label,
                    DataType = dataType,
                    IsSystem = true,
                    IsCollection = isCollection
                });
            }
            return result;
        }
    }

    public class SaveFormRequest
    {
        public Guid EntityId { get; set; }
        public Guid? FormId { get; set; }
        public string LayoutJson { get; set; }
        
        // Новые поля
        public string? Title { get; set; }
        public FormType? Type { get; set; }
    }
}