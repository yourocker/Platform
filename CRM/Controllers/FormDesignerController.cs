using Core.Data;
using Core.Entities.Platform;
using Core.FormEngine.Domain;
using Core.FormEngine.Schema; 
using CRM.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using Core.Entities.CRM;
using System.Text.Json; 

namespace CRM.Controllers
{
    /// <summary>
    /// Контроллер визуального конструктора форм.
    /// Отвечает за рендеринг редактора и API сохранения структуры.
    /// </summary>
    public class FormDesignerController : Controller
    {
        private readonly AppDbContext _context;

        public FormDesignerController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Основная страница конструктора.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(Guid entityId, Guid? formId = null)
        {
            var entityDef = await _context.AppDefinitions
                .Include(x => x.Fields)
                .FirstOrDefaultAsync(x => x.Id == entityId);

            if (entityDef == null)
                return NotFound($"Entity with ID {entityId} not found");

            // 1. Собираем Динамические поля (из БД)
            var designerFields = entityDef.Fields.Select(f => new DesignerFieldDto
            {
                Name = f.SystemName,
                Label = f.Label,
                DataType = f.DataType.ToString(),
                IsSystem = false,
                IsCollection = f.IsArray
            }).ToList();

            // 2. Собираем Системные поля (из C# класса)
            if (entityDef.IsSystem && !string.IsNullOrEmpty(entityDef.EntityCode))
            {
                var systemFields = GetSystemProperties(entityDef.EntityCode);
                designerFields.InsertRange(0, systemFields);
            }

            var existingForms = await _context.AppFormDefinitions
                .Where(x => x.AppDefinitionId == entityId)
                .OrderBy(x => x.Name)
                .ToListAsync();

            AppFormDefinition? currentForm = null;
            if (formId.HasValue)
            {
                currentForm = existingForms.FirstOrDefault(x => x.Id == formId.Value);
            }

            var viewModel = new FormDesignerViewModel
            {
                EntityId = entityDef.Id,
                EntityName = entityDef.Name,
                AvailableFields = designerFields,
                ExistingForms = existingForms,
                CurrentForm = currentForm
            };

            return View(viewModel);
        }

        /// <summary>
        /// API для сохранения структуры формы (JSON) в базу данных.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveConfig([FromBody] SaveFormRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.LayoutJson))
                return BadRequest("Пустой запрос");

            // 1. Пробуем десериализовать JSON, чтобы убедиться в валидности структуры
            FormLayoutSchema? schema;
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                schema = JsonSerializer.Deserialize<FormLayoutSchema>(request.LayoutJson, options);
            }
            catch (Exception ex)
            {
                return BadRequest($"Ошибка валидации JSON: {ex.Message}");
            }

            if (schema == null) return BadRequest("Некорректный JSON схемы");

            AppFormDefinition? formDef;

            // 2. Логика Обновления или Создания
            if (request.FormId.HasValue)
            {
                // --- ОБНОВЛЕНИЕ ---
                formDef = await _context.AppFormDefinitions
                    .FirstOrDefaultAsync(x => x.Id == request.FormId.Value);

                if (formDef == null) return NotFound("Форма не найдена");

                // Обновляем только структуру
                formDef.Layout = schema;
                formDef.UpdatedAt = DateTime.UtcNow;
                
                _context.Update(formDef);
                await _context.SaveChangesAsync();
                
                return Ok(new { message = "Форма обновлена" });
            }
            else
            {
                // --- СОЗДАНИЕ НОВОЙ ---
                
                // Генерируем уникальный код и имя
                // В будущем здесь можно добавить проверку на уникальность или брать имя из UI
                var newFormCode = $"custom_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                var newName = $"Новая форма {DateTime.Now:dd.MM HH:mm}";

                formDef = new AppFormDefinition
                {
                    Id = Guid.NewGuid(),
                    AppDefinitionId = request.EntityId,
                    Name = newName,
                    FormCode = newFormCode,
                    IsDefault = false, // По умолчанию не главная
                    Layout = schema,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.AppFormDefinitions.Add(formDef);
                await _context.SaveChangesAsync();

                // Возвращаем URL для редиректа, чтобы JS перегрузил страницу на новую форму
                var redirectUrl = Url.Action("Index", new { entityId = request.EntityId, formId = formDef.Id });
                return Ok(new { message = "Форма создана", redirectUrl = redirectUrl });
            }
        }

        private List<DesignerFieldDto> GetSystemProperties(string entityCode)
        {
            Type? type = entityCode switch
            {
                "Contact" => typeof(Contact),
                // Добавим другие типы по мере необходимости
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

    /// <summary>
    /// DTO для получения данных от JS-конструктора
    /// </summary>
    public class SaveFormRequest
    {
        public Guid EntityId { get; set; }
        public Guid? FormId { get; set; }
        public string LayoutJson { get; set; }
    }
}