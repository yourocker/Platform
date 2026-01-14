using System;
using System.Linq;
using System.Threading.Tasks;
using Core.Data;
using Core.Entities.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering; // Нужно для SelectListItem

namespace CRM.Controllers;

public class GenericObjectsController(AppDbContext context, IWebHostEnvironment hostingEnvironment) : BasePlatformController(context, hostingEnvironment)
{
    // --- ВСПОМОГАТЕЛЬНЫЙ МЕТОД: ГАРАНТИРОВАННАЯ ЗАГРУЗКА ПОЛЕЙ ---
    // Мы используем его, чтобы не зависеть от скрытой логики BasePlatformController
    private async Task LoadDefinitionWithFields(string entityCode)
    {
        // 1. Явно грузим Definition + Fields
        var definition = await _context.AppDefinitions
            .Include(d => d.Fields)
            .FirstOrDefaultAsync(d => d.EntityCode == entityCode);

        if (definition != null)
        {
            // Сортируем поля и кладем в ViewBag, который ждет _DynamicFields.cshtml
            ViewBag.DynamicFields = definition.Fields.OrderBy(f => f.SortOrder).ToList();
            ViewBag.DefinitionName = definition.Name;

            // 2. Загружаем данные для выпадающих списков (EntityLink)
            var lookupData = new Dictionary<string, List<SelectListItem>>();
            
            foreach (var field in definition.Fields.Where(f => f.DataType == FieldDataType.EntityLink))
            {
                var items = new List<SelectListItem>();
                
                // Пробуем найти сущности по коду цели (TargetEntityCode)
                // Сначала ищем в GenericObjects
                var genericItems = await _context.GenericObjects
                    .Where(g => g.EntityCode == field.TargetEntityCode)
                    .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                    .ToListAsync();
                
                if (genericItems.Any())
                {
                    items.AddRange(genericItems);
                }
                else
                {
                    // Если в Generic пусто, пробуем системные справочники (пример для Сотрудников/Отделов)
                    // Добавь сюда другие системные сущности по мере необходимости
                    if (field.TargetEntityCode == "Employees")
                    {
                        items = await _context.Employees
                            .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
                            .ToListAsync();
                    }
                    else if (field.TargetEntityCode == "Departments")
                    {
                        items = await _context.Departments
                            .Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name })
                            .ToListAsync();
                    }
                    else if (field.TargetEntityCode == "Patients")
                    {
                        items = await _context.Patients
                            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.FullName })
                            .ToListAsync();
                    }
                }
                
                lookupData[field.SystemName] = items;
            }

            ViewBag.LookupData = lookupData;
        }
    }
    // -------------------------------------------------------------

    public async Task<IActionResult> Index(string entityCode)
    {
        if (string.IsNullOrEmpty(entityCode)) return NotFound();
        
        var definition = await _context.AppDefinitions
            .Include(d => d.Fields)
            .OrderBy(d => d.Name)
            .FirstOrDefaultAsync(d => d.EntityCode == entityCode);

        if (definition == null) return NotFound();

        ViewBag.Definition = definition;
        ViewBag.EntityCode = entityCode;

        var objects = await _context.GenericObjects
            .Where(o => o.EntityCode == entityCode)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        // СБОР ВСЕХ GUID (как было у тебя)
        var allGuids = new HashSet<Guid>();
        var guidRegex = new Regex(@"([a-fA-F0-9]{8}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{12})");

        foreach (var obj in objects)
        {
            if (!string.IsNullOrEmpty(obj.Properties))
            {
                var matches = guidRegex.Matches(obj.Properties);
                foreach (Match match in matches)
                {
                    if (Guid.TryParse(match.Value, out Guid g)) allGuids.Add(g);
                }
            }
        }

        var namesMap = new Dictionary<Guid, string>();
        if (allGuids.Count > 0)
        {
            var genericNames = await _context.GenericObjects.Where(g => allGuids.Contains(g.Id)).Select(g => new { g.Id, g.Name }).ToListAsync();
            foreach (var n in genericNames) namesMap[n.Id] = n.Name;

            var patientNames = await _context.Patients.Where(p => allGuids.Contains(p.Id)).Select(p => new { p.Id, Name = p.FullName }).ToListAsync();
            foreach (var n in patientNames) namesMap[n.Id] = n.Name;

            var employeeNames = await _context.Employees.Where(e => allGuids.Contains(e.Id)).Select(e => new { e.Id, Name = e.FullName }).ToListAsync();
            foreach (var n in employeeNames) namesMap[n.Id] = n.Name;

            var deptNames = await _context.Departments.Where(d => allGuids.Contains(d.Id)).Select(d => new { d.Id, d.Name }).ToListAsync();
            foreach (var n in deptNames) namesMap[n.Id] = n.Name;
        }

        ViewBag.NamesMap = namesMap;
        return View(objects);
    }

    public async Task<IActionResult> Create(string entityCode)
    {
        if (string.IsNullOrEmpty(entityCode)) return NotFound();

        // ИСПОЛЬЗУЕМ ЯВНУЮ ЗАГРУЗКУ
        await LoadDefinitionWithFields(entityCode);
        
        ViewBag.EntityCode = entityCode;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GenericObject obj, IFormCollection form)
    {
        obj.CreatedAt = DateTime.UtcNow;

        // Сохраняем свойства (метод из базового контроллера, оставляем если он работает с файлами)
        await SaveDynamicProperties(obj, form, obj.EntityCode);

        if (ModelState.IsValid)
        {
            obj.Id = Guid.NewGuid();
            _context.Add(obj);
            await _context.SaveChangesAsync();
            
            FinalizeDynamicFilePaths(obj, obj.EntityCode, obj.Id.ToString());
            await _context.SaveChangesAsync();
            
            return Redirect($"/Data/{obj.EntityCode}");
        }

        // ПРИ ОШИБКЕ ВАЛИДАЦИИ СНОВА ГРУЗИМ ПОЛЯ
        await LoadDefinitionWithFields(obj.EntityCode);
        
        ViewBag.EntityCode = obj.EntityCode;
        return View(obj);
    }
    
    public async Task<IActionResult> Edit(Guid id)
    {
        var obj = await _context.GenericObjects.FindAsync(id);
        if (obj == null) return NotFound();

        // !!! КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ !!!
        // Загружаем поля явно по EntityCode объекта
        await LoadDefinitionWithFields(obj.EntityCode);
        
        ViewBag.EntityCode = obj.EntityCode;
        return View(obj);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, GenericObject incomingObj, IFormCollection form)
    {
        if (id != incomingObj.Id) return NotFound();

        var dbObj = await _context.GenericObjects.FindAsync(id);
        if (dbObj == null) return NotFound();

        // Накладываем изменения имени
        dbObj.Name = incomingObj.Name;

        // Накладываем динамические свойства
        await SaveDynamicProperties(dbObj, form, dbObj.EntityCode);

        if (ModelState.IsValid)
        {
            try 
            {
                FinalizeDynamicFilePaths(dbObj, dbObj.EntityCode, dbObj.Id.ToString());
                _context.Update(dbObj);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.GenericObjects.Any(e => e.Id == id)) return NotFound();
                else throw;
            }
            return Redirect($"/Data/{dbObj.EntityCode}");
        }

        // ПРИ ОШИБКЕ ВАЛИДАЦИИ СНОВА ГРУЗИМ ПОЛЯ
        await LoadDefinitionWithFields(dbObj.EntityCode);
        
        ViewBag.EntityCode = dbObj.EntityCode;
        return View(dbObj);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var obj = await _context.GenericObjects.FindAsync(id);
        if (obj == null) return NotFound();

        var entityCode = obj.EntityCode;
        _context.GenericObjects.Remove(obj);
        await _context.SaveChangesAsync();

        return Redirect($"/Data/{entityCode}");
    }
}