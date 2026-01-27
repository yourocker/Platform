using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.Data;
using Core.Entities.Company;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;

namespace CRM.Controllers
{
    public class PositionsController : BasePlatformController
    {
        public PositionsController(AppDbContext context, IWebHostEnvironment hostingEnvironment) 
            : base(context, hostingEnvironment)
        {
        }

        public async Task<IActionResult> Index(string? searchString, int? pageNumber, int? pageSize, Dictionary<string, string> filters)
        {
            await LoadDynamicFields("Position");
            var query = _context.Positions.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
                query = query.Where(p => EF.Functions.ILike(p.Name, $"%{searchString}%"));

            if (filters != null && filters.Any())
            {
                foreach (var filter in filters)
                {
                    if (string.IsNullOrWhiteSpace(filter.Value)) continue;
                    if (filter.Key == "f_Name")
                        query = query.Where(p => EF.Functions.ILike(p.Name, $"%{filter.Value}%"));
                    else if (filter.Key.StartsWith("f_dyn_"))
                    {
                        var fieldName = filter.Key.Replace("f_dyn_", "");
                        query = query.Where(p => EF.Functions.ILike(p.Properties, $"%\"{fieldName}\":%\"{filter.Value}\"%"));
                    }
                }
            }

            int actualPageSize = pageSize ?? 10;
            int actualPageNumber = pageNumber ?? 1;
            int totalItems = await query.CountAsync();
            
            var positions = await query
                .OrderBy(p => p.Name)
                .Skip((actualPageNumber - 1) * actualPageSize)
                .Take(actualPageSize)
                .ToListAsync();

            ViewBag.TotalItems = totalItems;
            ViewBag.PageNumber = actualPageNumber;
            ViewBag.PageSize = actualPageSize;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)actualPageSize);
            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentFilters = filters ?? new Dictionary<string, string>();

            return View(positions);
        }

        public async Task<IActionResult> Create() { await LoadDynamicFields("Position"); return View(); }

        [HttpPost] [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Position position, IFormCollection form)
        {
            await SaveDynamicProperties(position, form, "Position");
            if (ModelState.IsValid)
            {
                position.Id = Guid.NewGuid();
                _context.Add(position);
                await _context.SaveChangesAsync();
                FinalizeDynamicFilePaths(position, "Position", position.Id.ToString());
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await LoadDynamicFields("Position");
            return View(position);
        }

        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();
            var position = await _context.Positions.FindAsync(id);
            if (position == null) return NotFound();
            await LoadDynamicFields("Position");
            return View(position);
        }

        [HttpPost] [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, Position position, IFormCollection form)
        {
            if (id != position.Id) return NotFound();
            await SaveDynamicProperties(position, form, "Position");
            if (ModelState.IsValid)
            {
                try {
                    FinalizeDynamicFilePaths(position, "Position", position.Id.ToString());
                    _context.Update(position);
                    await _context.SaveChangesAsync();
                } catch (DbUpdateConcurrencyException) {
                    if (!PositionExists(position.Id)) return NotFound(); else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            await LoadDynamicFields("Position");
            return View(position);
        }

        // --- GET: ПОДТВЕРЖДЕНИЕ УДАЛЕНИЯ ---
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();
            var position = await _context.Positions.FirstOrDefaultAsync(m => m.Id == id);
            if (position == null) return NotFound();
            
            ViewBag.HasEmployees = await _context.StaffAppointments.AnyAsync(a => a.PositionId == id);
            return View(position);
        }

        // --- POST: САМО УДАЛЕНИЕ ---
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var position = await _context.Positions.FindAsync(id);
            if (position != null)
            {
                var hasEmployees = await _context.StaffAppointments.AnyAsync(a => a.PositionId == id);
                if (!hasEmployees) 
                {
                    _context.Positions.Remove(position);
                    await _context.SaveChangesAsync();
                }
            }
            return RedirectToAction(nameof(Index));
        }

        private bool PositionExists(Guid id) => _context.Positions.Any(e => e.Id == id);
    }
}