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
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CRM.Controllers;

// Устанавливаем базовый маршрут для всех действий контроллера
[Route("Data")]
public class GenericObjectsController(AppDbContext context, IWebHostEnvironment hostingEnvironment) : BasePlatformController(context, hostingEnvironment)
{
    private async Task LoadDefinitionWithFields(string entityCode)
    {
        var definition = await _context.AppDefinitions
            .Include(d => d.Fields)
            .FirstOrDefaultAsync(d => d.EntityCode == entityCode);

        if (definition != null)
        {
            ViewBag.DynamicFields = definition.Fields.OrderBy(f => f.SortOrder).ToList();
            ViewBag.DefinitionName = definition.Name;

            var lookupData = new Dictionary<string, List<SelectListItem>>();
            
            foreach (var field in definition.Fields.Where(f => f.DataType == FieldDataType.EntityLink))
            {
                var items = new List<SelectListItem>();
                var genericItems = await _context.GenericObjects
                    .Where(g => g.EntityCode == field.TargetEntityCode)
                    .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                    .ToListAsync();
                
                if (genericItems.Any()) items.AddRange(genericItems);
                else
                {
                    if (field.TargetEntityCode == "Employees")
                        items = await _context.Employees.Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName }).ToListAsync();
                    else if (field.TargetEntityCode == "Departments")
                        items = await _context.Departments.Select(d => new SelectListItem { Value = d.Id.ToString(), Text = d.Name }).ToListAsync();
                    else if (field.TargetEntityCode == "Patients")
                        items = await _context.Patients.Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.FullName }).ToListAsync();
                }
                lookupData[field.SystemName] = items;
            }
            ViewBag.LookupData = lookupData;
        }
    }

    // Маршрут: /Data/{entityCode} (например /Data/Sklad)
    [HttpGet("{entityCode}")]
    public async Task<IActionResult> Index(string entityCode, string? searchString, int? pageNumber, int? pageSize, [FromQuery] Dictionary<string, string>? filters)
    {
        if (string.IsNullOrEmpty(entityCode)) return NotFound();
        
        var definition = await _context.AppDefinitions
            .Include(d => d.Fields)
            .FirstOrDefaultAsync(d => d.EntityCode == entityCode);

        if (definition == null) return NotFound();

        ViewBag.Definition = definition;
        ViewBag.EntityCode = entityCode;
        ViewBag.DynamicFields = definition.Fields.OrderBy(f => f.SortOrder).ToList();

        var query = _context.GenericObjects
            .Where(o => o.EntityCode == entityCode)
            .AsQueryable();

        // 1. Быстрый поиск по системному имени
        if (!string.IsNullOrWhiteSpace(searchString))
        {
            query = query.Where(o => EF.Functions.ILike(o.Name, $"%{searchString}%"));
        }

        // 2. Тотальная фильтрация по динамическим полям (Properties)
        if (filters != null && filters.Any())
        {
            foreach (var filter in filters)
            {
                if (string.IsNullOrWhiteSpace(filter.Value)) continue;

                if (filter.Key.Contains("f_Name"))
                {
                    query = query.Where(o => EF.Functions.ILike(o.Name, $"%{filter.Value}%"));
                }
                else if (filter.Key.Contains("f_dyn_"))
                {
                    var fieldName = filter.Key.Split("f_dyn_").Last();
                    query = query.Where(o => EF.Functions.ILike(o.Properties, $"%\"{fieldName}\":%\"{filter.Value}\"%"));
                }
            }
        }

        // 3. Честная пагинация
        int actualPageSize = pageSize ?? 10;
        int actualPageNumber = pageNumber ?? 1;
        int totalItems = await query.CountAsync();

        var objects = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((actualPageNumber - 1) * actualPageSize)
            .Take(actualPageSize)
            .ToListAsync();

        // 4. Сбор GUID для текущей страницы (твоя логика Regex)
        var allGuids = new HashSet<Guid>();
        var guidRegex = new Regex(@"([a-fA-F0-9]{8}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{4}[-][a-fA-F0-9]{12})");

        foreach (var obj in objects.Where(o => !string.IsNullOrEmpty(o.Properties)))
        {
            var matches = guidRegex.Matches(obj.Properties);
            foreach (Match match in matches)
            {
                if (Guid.TryParse(match.Value, out Guid g)) allGuids.Add(g);
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
        ViewBag.TotalItems = totalItems;
        ViewBag.PageNumber = actualPageNumber;
        ViewBag.PageSize = actualPageSize;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)actualPageSize);
        ViewBag.CurrentSearch = searchString;
        ViewBag.CurrentFilters = filters ?? new Dictionary<string, string>();

        return View(objects);
    }

    // Маршрут: /Data/{entityCode}/Create
    [HttpGet("{entityCode}/Create")]
    public async Task<IActionResult> Create(string entityCode)
    {
        if (string.IsNullOrEmpty(entityCode)) return NotFound();
        await LoadDefinitionWithFields(entityCode);
        ViewBag.EntityCode = entityCode;
        return View();
    }

    [HttpPost("{entityCode}/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GenericObject obj, IFormCollection form)
    {
        obj.CreatedAt = DateTime.UtcNow;
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
        await LoadDefinitionWithFields(obj.EntityCode);
        ViewBag.EntityCode = obj.EntityCode;
        return View(obj);
    }
    
    // Маршрут: /Data/Edit/{id}
    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var obj = await _context.GenericObjects.FindAsync(id);
        if (obj == null) return NotFound();
        await LoadDefinitionWithFields(obj.EntityCode);
        ViewBag.EntityCode = obj.EntityCode;
        return View(obj);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, GenericObject incomingObj, IFormCollection form)
    {
        if (id != incomingObj.Id) return NotFound();
        var dbObj = await _context.GenericObjects.FindAsync(id);
        if (dbObj == null) return NotFound();
        dbObj.Name = incomingObj.Name;
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
        await LoadDefinitionWithFields(dbObj.EntityCode);
        ViewBag.EntityCode = dbObj.EntityCode;
        return View(dbObj);
    }

    // Маршрут: /Data/Delete/{id}
    [HttpPost("Delete/{id}")]
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