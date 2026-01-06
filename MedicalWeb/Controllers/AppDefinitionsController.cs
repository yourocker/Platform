using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedicalBot.Data;
using MedicalBot.Entities.Platform;

namespace MedicalWeb.Controllers
{
    public class AppDefinitionsController : Controller
    {
        private readonly AppDbContext _context;

        public AppDefinitionsController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Список всех сущностей (Сотрудники, Пациенты, Приложения)
        public async Task<IActionResult> Index()
        {
            var apps = await _context.AppDefinitions
                .Include(a => a.Fields)
                .OrderByDescending(a => a.IsSystem) // Сначала системные
                .ToListAsync();
            return View(apps);
        }

        // 2. Список полей конкретной сущности
        public async Task<IActionResult> Fields(Guid id)
        {
            var app = await _context.AppDefinitions
                .Include(a => a.Fields)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (app == null) return NotFound();

            return View(app);
        }

        // 3. POST: Добавление нового поля
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddField(Guid appDefinitionId, string label, string systemName, FieldDataType dataType, bool isRequired)
        {
            // Простейшая валидация системного имени (только латиница и подчеркивание)
            if (string.IsNullOrEmpty(systemName))
            {
                return BadRequest("Системное имя обязательно");
            }

            var field = new AppFieldDefinition
            {
                Id = Guid.NewGuid(),
                AppDefinitionId = appDefinitionId,
                Label = label,
                SystemName = systemName.ToLower().Trim(),
                DataType = dataType,
                IsRequired = isRequired,
                SortOrder = await _context.AppFieldDefinitions
                    .Where(f => f.AppDefinitionId == appDefinitionId)
                    .CountAsync() + 1
            };

            _context.AppFieldDefinitions.Add(field);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Fields), new { id = appDefinitionId });
        }

        // 4. POST: Удаление поля
        [HttpPost]
        public async Task<IActionResult> DeleteField(Guid id)
        {
            var field = await _context.AppFieldDefinitions.FindAsync(id);
            if (field != null)
            {
                var appId = field.AppDefinitionId;
                _context.AppFieldDefinitions.Remove(field);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Fields), new { id = appId });
            }
            return NotFound();
        }
    }
}