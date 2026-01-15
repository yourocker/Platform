using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Core.Data;
using Core.Entities.CRM;
using Core.Entities.Platform;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRM.Controllers
{
    // Вспомогательная модель для отображения дерева в таблице
    public class ServiceTreeItem
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; } // ДОБАВЛЕНО для связи в JS
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Item"; // "Category" или "Item"
        public decimal? Price { get; set; }
        public int Level { get; set; }
        public string? Properties { get; set; }
    }

    public class ServicesController : Controller
    {
        private readonly AppDbContext _context;

        public ServicesController(AppDbContext context)
        {
            _context = context;
        }

        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ---

        private async Task LoadViewData(string entityCode = "ServiceItem")
        {
            // Загружаем поля конструктора для конкретной сущности (Услуга или Категория)
            var appDef = await _context.AppDefinitions
                .Include(a => a.Fields)
                .FirstOrDefaultAsync(a => a.EntityCode == entityCode);

            if (appDef != null)
            {
                ViewBag.DynamicFields = appDef.Fields.OrderBy(f => f.SortOrder).ToList();
            }
            
            // Список категорий для выпадающих списков
            var categories = await _context.ServiceCategories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            ViewBag.EntityCode = entityCode;
        }

        private Dictionary<string, object> ExtractDynamicProps()
        {
            var dict = new Dictionary<string, object>();
            foreach (var key in Request.Form.Keys.Where(k => k.StartsWith("DynamicProps[")))
            {
                var systemName = key.Replace("DynamicProps[", "").Replace("]", "");
                var values = Request.Form[key].ToList();
                dict[systemName] = values.Count > 1 ? values : (values.FirstOrDefault() ?? "");
            }
            return dict;
        }

        // --- УСЛУГИ (ServiceItem) ---

        public async Task<IActionResult> Index(string searchString)
        {
            // Загружаем поля для заголовков таблицы
            await LoadViewData("ServiceItem");

            var allCategories = await _context.ServiceCategories.ToListAsync();
            var allItems = await _context.ServiceItems.Include(i => i.Category).ToListAsync();

            var treeResult = new List<ServiceTreeItem>();

            if (!string.IsNullOrEmpty(searchString))
            {
                // При поиске показываем плоский список (дерево скрываем для удобства поиска)
                var s = searchString.Trim();
                var filteredItems = allItems.Where(i => 
                    EF.Functions.ILike(i.Name, $"%{s}%") || 
                    EF.Functions.ILike(i.Category.Name, $"%{s}%") ||
                    (i.Properties != null && i.Properties.Contains(s, StringComparison.OrdinalIgnoreCase))
                ).Select(i => new ServiceTreeItem {
                    Id = i.Id, 
                    ParentId = i.CategoryId, // Чтобы в поиске тоже была видна привязка
                    Name = i.Name, 
                    Type = "Item", 
                    Price = i.Price, 
                    Level = 0, 
                    Properties = i.Properties
                });
                treeResult.AddRange(filteredItems);
            }
            else
            {
                // Строим дерево рекурсивно
                BuildTree(null, 0, allCategories, allItems, treeResult);
            }

            ViewBag.CurrentSearch = searchString;
            return View(treeResult);
        }

        private void BuildTree(Guid? parentId, int level, List<ServiceCategory> cats, List<ServiceItem> items, List<ServiceTreeItem> result)
        {
            if (level >= 10) return; // Ограничение вложенности 10 уровней

            var currentLevelCats = cats.Where(c => c.ParentCategoryId == parentId).OrderBy(c => c.Name);
            foreach (var cat in currentLevelCats)
            {
                // Добавляем категорию
                result.Add(new ServiceTreeItem { 
                    Id = cat.Id, 
                    ParentId = parentId, // УСТАНОВЛЕНО для сворачивания
                    Name = cat.Name, 
                    Type = "Category", 
                    Level = level, 
                    Properties = cat.Properties 
                });
                
                // Рекурсивно добавляем подкатегории
                BuildTree(cat.Id, level + 1, cats, items, result);
                
                // Добавляем услуги, принадлежащие этой категории
                var currentLevelItems = items.Where(i => i.CategoryId == cat.Id).OrderBy(i => i.Name);
                foreach (var item in currentLevelItems)
                {
                    result.Add(new ServiceTreeItem { 
                        Id = item.Id, 
                        ParentId = cat.Id, // УСТАНОВЛЕНО для сворачивания
                        Name = item.Name, 
                        Type = "Item", 
                        Price = item.Price, 
                        Level = level + 1, 
                        Properties = item.Properties 
                    });
                }
            }

            // Обработка услуг в корне (без категории), если такие есть
            if (parentId == null)
            {
                var rootItems = items.Where(i => i.CategoryId == null || i.CategoryId == Guid.Empty || !cats.Any(c => c.Id == i.CategoryId));
                foreach (var item in rootItems)
                {
                    result.Add(new ServiceTreeItem { Id = item.Id, ParentId = null, Name = item.Name, Type = "Item", Price = item.Price, Level = 0, Properties = item.Properties });
                }
            }
        }

        public async Task<IActionResult> Create()
        {
            await LoadViewData("ServiceItem");
            return View(new ServiceItem { Id = Guid.NewGuid(), EntityCode = "ServiceItem" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceItem service)
        {
            var dynamicData = ExtractDynamicProps();
            if (dynamicData.Any()) service.Properties = JsonSerializer.Serialize(dynamicData);

            service.CreatedAt = DateTime.UtcNow;
            ModelState.Remove(nameof(service.EntityCode));
            ModelState.Remove(nameof(service.Category));

            if (ModelState.IsValid)
            {
                _context.Add(service);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await LoadViewData("ServiceItem");
            return View(service);
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var service = await _context.ServiceItems.FindAsync(id);
            if (service == null) return NotFound();
            await LoadViewData("ServiceItem");
            return View(service);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, ServiceItem service)
        {
            if (id != service.Id) return NotFound();

            var dynamicData = ExtractDynamicProps();
            service.Properties = dynamicData.Any() ? JsonSerializer.Serialize(dynamicData) : null;

            ModelState.Remove(nameof(service.EntityCode));
            ModelState.Remove(nameof(service.Category));

            if (ModelState.IsValid)
            {
                try
                {
                    var original = await _context.ServiceItems.FindAsync(id);
                    if (original == null) return NotFound();

                    original.Name = service.Name;
                    original.Price = service.Price;
                    original.CategoryId = service.CategoryId;
                    original.Properties = service.Properties;

                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException) { if (!ServiceExists(service.Id)) return NotFound(); throw; }
            }
            await LoadViewData("ServiceItem");
            return View(service);
        }

        // --- AJAX УДАЛЕНИЕ ---

        [HttpPost]
        public async Task<IActionResult> DeleteItem(Guid id)
        {
            var item = await _context.ServiceItems.FindAsync(id);
            if (item == null) return Json(new { success = false, message = "Услуга не найдена" });

            _context.ServiceItems.Remove(item);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCategory(Guid id)
        {
            var cat = await _context.ServiceCategories.FindAsync(id);
            if (cat == null) return Json(new { success = false, message = "Категория не найдена" });

            // Проверка: есть ли вложенные категории или товары
            bool hasChildren = await _context.ServiceCategories.AnyAsync(c => c.ParentCategoryId == id) 
                            || await _context.ServiceItems.AnyAsync(i => i.CategoryId == id);

            if (hasChildren)
                return Json(new { success = false, message = "Нельзя удалить раздел, пока в нем есть товары или подразделы." });

            _context.ServiceCategories.Remove(cat);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        private bool ServiceExists(Guid id) => _context.ServiceItems.Any(e => e.Id == id);

        // --- КАТЕГОРИИ (ServiceCategory) ---

        public async Task<IActionResult> CreateCategory()
        {
            await LoadViewData("ServiceCategory");
            return View(new ServiceCategory { Id = Guid.NewGuid(), EntityCode = "ServiceCategory" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(ServiceCategory category)
        {
            var dynamicData = ExtractDynamicProps();
            if (dynamicData.Any()) category.Properties = JsonSerializer.Serialize(dynamicData);

            category.CreatedAt = DateTime.UtcNow;
            ModelState.Remove(nameof(category.EntityCode));

            if (ModelState.IsValid)
            {
                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await LoadViewData("ServiceCategory");
            return View(category);
        }

        public async Task<IActionResult> EditCategory(Guid? id)
        {
            if (id == null) return NotFound();
            var category = await _context.ServiceCategories.FindAsync(id);
            if (category == null) return NotFound();
            await LoadViewData("ServiceCategory");
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(Guid id, ServiceCategory category)
        {
            if (id != category.Id) return NotFound();

            var dynamicData = ExtractDynamicProps();
            category.Properties = dynamicData.Any() ? JsonSerializer.Serialize(dynamicData) : null;

            ModelState.Remove(nameof(category.EntityCode));

            if (ModelState.IsValid)
            {
                try
                {
                    var original = await _context.ServiceCategories.FindAsync(id);
                    if (original == null) return NotFound();

                    original.Name = category.Name;
                    original.ParentCategoryId = category.ParentCategoryId;
                    original.Properties = category.Properties;

                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException) { if (!CategoryExists(category.Id)) return NotFound(); throw; }
            }
            await LoadViewData("ServiceCategory");
            return View(category);
        }

        private bool CategoryExists(Guid id) => _context.ServiceCategories.Any(e => e.Id == id);
    }
}