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
using ClosedXML.Excel;
using CRM.ViewModels;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace CRM.Controllers
{
    public class ServiceTreeItem
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Item";
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
            var appDef = await _context.AppDefinitions
                .Include(a => a.Fields)
                .FirstOrDefaultAsync(a => a.EntityCode == entityCode);

            if (appDef != null)
            {
                ViewBag.DynamicFields = appDef.Fields.OrderBy(f => f.SortOrder).ToList();
            }
            
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
        
        // Отображение страницы выбора файла
[HttpGet]
public IActionResult Import()
{
    return View();
}

// Обработка загруженного файла и сохранение во временную папку
[HttpPost]
public async Task<IActionResult> UploadForImport(IFormFile file)
{
    if (file == null || file.Length == 0) return BadRequest("Файл не выбран");

    // Путь к временной папке в wwwroot
    var tempDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "temp_imports");
    if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
    var filePath = Path.Combine(tempDir, fileName);

    using (var stream = new FileStream(filePath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    return RedirectToAction(nameof(Mapping), new { fileName });
}

// Страница сопоставления полей (Mapping)
[HttpGet]
public async Task<IActionResult> Mapping(string fileName)
{
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "temp_imports", fileName);
    if (!System.IO.File.Exists(filePath)) return NotFound("Файл не найден");

    var model = new ImportMappingViewModel { FileName = fileName };

    using (var workbook = new XLWorkbook(filePath))
    {
        var worksheet = workbook.Worksheet(1);
        var firstRow = worksheet.Row(1);
        
        // Читаем заголовки Excel (первая строка)
        foreach (var cell in firstRow.CellsUsed())
        {
            model.ExcelHeaders.Add(cell.Value.ToString());
        }

        // Читаем первые 3 строки данных для предпросмотра
        for (int r = 2; r <= 4; r++)
        {
            var row = worksheet.Row(r);
            if (row.IsEmpty()) break;
            var rowData = new List<string>();
            for (int c = 1; c <= model.ExcelHeaders.Count; c++)
            {
                rowData.Add(row.Cell(c).Value.ToString());
            }
            model.PreviewRows.Add(rowData);
        }
    }

    // Собираем список полей CRM, которые можно заполнить
    model.CrmFields.Add(new CrmFieldDefinition { SystemName = "DisplayId", DisplayName = "ID (для обновления существующих)" });
    model.CrmFields.Add(new CrmFieldDefinition { SystemName = "Name", DisplayName = "Название услуги", IsRequired = true });
    model.CrmFields.Add(new CrmFieldDefinition { SystemName = "Price", DisplayName = "Цена", IsRequired = true });

    // Поля разделов (10 уровней)
    for (int i = 1; i <= 10; i++)
    {
        model.CrmFields.Add(new CrmFieldDefinition { SystemName = $"Category_L{i}", DisplayName = $"Раздел уровень {i}" });
    }

    // Динамические поля из конструктора
    var appDef = await _context.AppDefinitions.Include(a => a.Fields)
        .FirstOrDefaultAsync(a => a.EntityCode == "ServiceItem");
    
    if (appDef != null)
    {
        foreach (var f in appDef.Fields.OrderBy(x => x.SortOrder))
        {
            model.CrmFields.Add(new CrmFieldDefinition { SystemName = "Prop_" + f.SystemName, DisplayName = f.Label });
        }
    }

    return View(model);
}

[HttpPost]
public async Task<IActionResult> ExecuteImport(string fileName, Dictionary<string, string> mappings)
{
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "temp_imports", fileName);
    if (!System.IO.File.Exists(filePath)) return NotFound("Файл импорта не найден. Попробуйте загрузить заново.");

    int createdCount = 0;
    int updatedCount = 0;

    using (var workbook = new XLWorkbook(filePath))
    {
        var worksheet = workbook.Worksheet(1);
        var rows = worksheet.RowsUsed().Skip(1); // Пропускаем заголовки

        foreach (var row in rows)
        {
            // 1. Определяем категорию (проходим по 10 уровням)
            Guid? currentParentId = null;
            for (int i = 1; i <= 10; i++)
            {
                var key = $"Category_L{i}";
                if (mappings.TryGetValue(key, out var colIdxStr) && int.TryParse(colIdxStr, out int colIdx))
                {
                    var catName = row.Cell(colIdx + 1).Value.ToString().Trim();
                    if (string.IsNullOrEmpty(catName)) break;

                    var category = await _context.ServiceCategories
                        .FirstOrDefaultAsync(c => c.Name == catName && c.ParentCategoryId == currentParentId);

                    if (category == null)
                    {
                        category = new ServiceCategory 
                        { 
                            Id = Guid.NewGuid(), 
                            Name = catName, 
                            ParentCategoryId = currentParentId,
                            CreatedAt = DateTime.UtcNow,
                            EntityCode = "ServiceCategory"
                        };
                        _context.ServiceCategories.Add(category);
                        await _context.SaveChangesAsync();
                    }
                    currentParentId = category.Id;
                }
            }

            // 2. Получаем основные данные услуги
            string name = "";
            if (mappings.TryGetValue("Name", out var nameIdx) && int.TryParse(nameIdx, out int nIdx))
                name = row.Cell(nIdx + 1).Value.ToString().Trim();

            if (string.IsNullOrEmpty(name)) continue; // Пропускаем строки без названия

            decimal price = 0;
            if (mappings.TryGetValue("Price", out var priceIdx) && int.TryParse(priceIdx, out int pIdx))
                decimal.TryParse(row.Cell(pIdx + 1).Value.ToString(), out price);

            int displayId = 0;
            if (mappings.TryGetValue("DisplayId", out var dIdIdx) && int.TryParse(dIdIdx, out int dIdx))
                int.TryParse(row.Cell(dIdx + 1).Value.ToString(), out displayId);

            // 3. Ищем существующую услугу (по ID или по Названию в категории)
            ServiceItem item = null;
            if (displayId > 0)
            {
                item = await _context.ServiceItems.FirstOrDefaultAsync(x => x.DisplayId == displayId);
            }
            
            if (item == null)
            {
                item = await _context.ServiceItems.FirstOrDefaultAsync(x => x.Name == name && x.CategoryId == currentParentId);
            }

            bool isNew = (item == null);
            if (isNew)
            {
                item = new ServiceItem { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, EntityCode = "ServiceItem" };
                _context.ServiceItems.Add(item);
            }

            item.Name = name;
            item.Price = price;
            item.CategoryId = (Guid)currentParentId!;

            // 4. Обработка динамических полей
            var dynamicProps = new Dictionary<string, object>();
            foreach (var map in mappings.Where(m => m.Key.StartsWith("Prop_") && !string.IsNullOrEmpty(m.Value)))
            {
                if (int.TryParse(map.Value, out int propColIdx))
                {
                    var propSystemName = map.Key.Replace("Prop_", "");
                    var propValue = row.Cell(propColIdx + 1).Value.ToString();
                    dynamicProps[propSystemName] = propValue;
                }
            }

            if (dynamicProps.Any())
                item.Properties = JsonSerializer.Serialize(dynamicProps);

            if (isNew) createdCount++; else updatedCount++;
        }
        await _context.SaveChangesAsync();
    }

    // Удаляем временный файл
    System.IO.File.Delete(filePath);

    TempData["ImportResult"] = $"Импорт завершен. Создано: {createdCount}, Обновлено: {updatedCount}";
    return RedirectToAction(nameof(Index));
}

        // --- ЭКСПОРТ (ClosedXML - MIT License) ---

        public async Task<IActionResult> Export()
        {
            var appDef = await _context.AppDefinitions
                .Include(a => a.Fields)
                .FirstOrDefaultAsync(a => a.EntityCode == "ServiceItem");
            
            var dynamicFields = appDef?.Fields.OrderBy(f => f.SortOrder).ToList() ?? new List<AppFieldDefinition>();
            var categories = await _context.ServiceCategories.ToListAsync();
            var items = await _context.ServiceItems.Include(i => i.Category).ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Услуги");

                int col = 1;
                worksheet.Cell(1, col++).Value = "ID";
                
                for (int i = 1; i <= 10; i++)
                {
                    worksheet.Cell(1, col++).Value = $"Раздел {i}";
                }

                worksheet.Cell(1, col++).Value = "Название услуги";
                worksheet.Cell(1, col++).Value = "Цена";

                foreach (var field in dynamicFields)
                {
                    worksheet.Cell(1, col++).Value = field.SystemName;
                }

                // Стилизация шапки
                var headerRange = worksheet.Range(1, 1, 1, col - 1);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

                int row = 2;
                foreach (var item in items)
                {
                    col = 1;
                    worksheet.Cell(row, col++).Value = item.DisplayId;

                    var path = GetCategoryPath(item.CategoryId, categories);
                    for (int i = 0; i < 10; i++)
                    {
                        worksheet.Cell(row, col++).Value = i < path.Count ? path[i] : "";
                    }

                    worksheet.Cell(row, col++).Value = item.Name;
                    worksheet.Cell(row, col++).Value = item.Price;

                    if (!string.IsNullOrEmpty(item.Properties))
                    {
                        var props = JsonSerializer.Deserialize<Dictionary<string, object>>(item.Properties);
                        foreach (var field in dynamicFields)
                        {
                            if (props != null && props.TryGetValue(field.SystemName, out var val))
                            {
                                worksheet.Cell(row, col++).Value = val?.ToString();
                            }
                            col++;
                        }
                    }
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    string fileName = $"Services_Export_{DateTime.Now:yyyyMMddHHmm}.xlsx";
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
        }

        private List<string> GetCategoryPath(Guid? categoryId, List<ServiceCategory> allCats)
        {
            var path = new List<string>();
            var current = allCats.FirstOrDefault(c => c.Id == categoryId);
            
            while (current != null)
            {
                path.Insert(0, current.Name);
                current = allCats.FirstOrDefault(c => c.Id == current.ParentCategoryId);
            }
            return path;
        }

        // --- УСЛУГИ ---

        public async Task<IActionResult> Index(string searchString)
        {
            await LoadViewData("ServiceItem");

            var allCategories = await _context.ServiceCategories.ToListAsync();
            var allItems = await _context.ServiceItems.Include(i => i.Category).ToListAsync();

            var treeResult = new List<ServiceTreeItem>();

            if (!string.IsNullOrEmpty(searchString))
            {
                var s = searchString.Trim();
                var filteredItems = allItems.Where(i => 
                    EF.Functions.ILike(i.Name, $"%{s}%") || 
                    (i.Category != null && EF.Functions.ILike(i.Category.Name, $"%{s}%")) ||
                    (i.Properties != null && i.Properties.Contains(s, StringComparison.OrdinalIgnoreCase))
                ).Select(i => new ServiceTreeItem {
                    Id = i.Id, 
                    ParentId = i.CategoryId,
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
                BuildTree(null, 0, allCategories, allItems, treeResult);
            }

            ViewBag.CurrentSearch = searchString;
            return View(treeResult);
        }

        private void BuildTree(Guid? parentId, int level, List<ServiceCategory> cats, List<ServiceItem> items, List<ServiceTreeItem> result)
        {
            if (level >= 10) return;

            var currentLevelCats = cats.Where(c => c.ParentCategoryId == parentId).OrderBy(c => c.Name);
            foreach (var cat in currentLevelCats)
            {
                result.Add(new ServiceTreeItem { 
                    Id = cat.Id, 
                    ParentId = parentId,
                    Name = cat.Name, 
                    Type = "Category", 
                    Level = level, 
                    Properties = cat.Properties 
                });
                
                BuildTree(cat.Id, level + 1, cats, items, result);
                
                var currentLevelItems = items.Where(i => i.CategoryId == cat.Id).OrderBy(i => i.Name);
                foreach (var item in currentLevelItems)
                {
                    result.Add(new ServiceTreeItem { 
                        Id = item.Id, 
                        ParentId = cat.Id,
                        Name = item.Name, 
                        Type = "Item", 
                        Price = item.Price, 
                        Level = level + 1, 
                        Properties = item.Properties 
                    });
                }
            }

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

            bool hasChildren = await _context.ServiceCategories.AnyAsync(c => c.ParentCategoryId == id) 
                            || await _context.ServiceItems.AnyAsync(i => i.CategoryId == id);

            if (hasChildren)
                return Json(new { success = false, message = "Нельзя удалить раздел, пока в нем есть товары или подразделы." });

            _context.ServiceCategories.Remove(cat);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        private bool ServiceExists(Guid id) => _context.ServiceItems.Any(e => e.Id == id);

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