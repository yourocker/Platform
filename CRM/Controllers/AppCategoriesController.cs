using System;
using System.Linq;
using System.Threading.Tasks;
using Core.Data;
using Core.Entities.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

namespace CRM.Controllers
{
    public class AppCategoriesController : BasePlatformController
    {
        public AppCategoriesController(AppDbContext context, IWebHostEnvironment hostingEnvironment) 
            : base(context, hostingEnvironment)
        {
        }

        // ИСПРАВЛЕНО: Теперь принимает параметры для унифицированного Index
        public async Task<IActionResult> Index(string searchString, int pageNumber = 1, int pageSize = 10)
        {
            var query = _context.AppCategories.AsQueryable();

            // 1. Логика поиска
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(c => c.Name.Contains(searchString));
            }

            // 2. Считаем общее кол-во для пагинации
            var totalItems = await query.CountAsync();
    
            // 3. ПРАВИЛЬНАЯ СОРТИРОВКА: 
            // Сначала системные (IsSystem = true), затем пользовательские (IsSystem = false), 
            // и внутри этих групп уже по SortOrder.
            var categories = await query
                .OrderByDescending(c => c.IsSystem) // True (1) выше чем False (0)
                .ThenBy(c => c.SortOrder)           // Затем по указанному порядку
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 4. Заполняем ViewBag для вьюхи
            ViewBag.TotalItems = totalItems;
            ViewBag.PageNumber = pageNumber;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            ViewBag.CurrentSearch = searchString;

            return View(categories);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppCategory category)
        {
            if (ModelState.IsValid)
            {
                category.Id = Guid.NewGuid();
                _context.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var category = await _context.AppCategories.FindAsync(id);
            if (category == null) return NotFound();
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, AppCategory category)
        {
            if (id != category.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(category);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var category = await _context.AppCategories
                .Include(c => c.Apps)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (category == null) return NotFound();

            if (category.Apps.Any())
            {
                ViewBag.CanDelete = false;
                ViewBag.Reason = "В данном разделе есть привязанные сущности. Сначала переместите их в другой раздел.";
            }
            else
            {
                ViewBag.CanDelete = true;
            }

            return View(category);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var category = await _context.AppCategories.FindAsync(id);
            if (category != null)
            {
                _context.AppCategories.Remove(category);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool CategoryExists(Guid id) => _context.AppCategories.Any(e => e.Id == id);
    }
}