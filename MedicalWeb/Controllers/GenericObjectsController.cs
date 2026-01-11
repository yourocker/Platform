using System;
using System.Linq;
using System.Threading.Tasks;
using MedicalBot.Data;
using MedicalBot.Entities.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

namespace MedicalWeb.Controllers;

public class GenericObjectsController(AppDbContext context, IWebHostEnvironment hostingEnvironment) : BasePlatformController(context, hostingEnvironment)
{
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

        return View(objects);
    }

    public async Task<IActionResult> Create(string entityCode)
    {
        if (string.IsNullOrEmpty(entityCode)) return NotFound();

        await LoadDynamicFields(entityCode);
        ViewBag.EntityCode = entityCode;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GenericObject obj, IFormCollection form)
    {
        obj.CreatedAt = DateTime.UtcNow;

        // 1. Сохраняем свойства во временные папки (temp)
        await SaveDynamicProperties(obj, form, obj.EntityCode);

        if (ModelState.IsValid)
        {
            obj.Id = Guid.NewGuid();
            _context.Add(obj);
            
            // 2. Первое сохранение, чтобы зафиксировать запись в БД
            await _context.SaveChangesAsync();
            
            // 3. Теперь, когда Id точно есть, переносим файлы из temp в /uploads/{code}/{id}/
            FinalizeDynamicFilePaths(obj, obj.EntityCode, obj.Id.ToString());
            
            // 4. Второе сохранение - обновляем JSON пути в БД
            await _context.SaveChangesAsync();
            
            return Redirect($"/Data/{obj.EntityCode}");
        }

        await LoadDynamicFields(obj.EntityCode);
        ViewBag.EntityCode = obj.EntityCode;
        return View(obj);
    }
    
    public async Task<IActionResult> Edit(Guid id)
    {
        var obj = await _context.GenericObjects.FindAsync(id);
        if (obj == null) return NotFound();

        await LoadDynamicFields(obj.EntityCode);
        ViewBag.EntityCode = obj.EntityCode;
        
        return View(obj);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, GenericObject incomingObj, IFormCollection form)
    {
        if (id != incomingObj.Id) return NotFound();

        // 1. Загружаем существующий объект (dbObj) с его старыми Properties
        var dbObj = await _context.GenericObjects.FindAsync(id);
        if (dbObj == null) return NotFound();

        // 2. Накладываем изменения из формы на dbObj (новые файлы пойдут в temp)
        await SaveDynamicProperties(dbObj, form, dbObj.EntityCode);

        if (ModelState.IsValid)
        {
            try 
            {
                // 3. Переносим новые файлы из temp в постоянную папку записи
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

        await LoadDynamicFields(dbObj.EntityCode);
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