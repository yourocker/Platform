using System;
using System.Linq;
using System.Threading.Tasks;
using MedicalBot.Data;
using MedicalBot.Entities.Company;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalWeb.Controllers
{
    public class PositionsController : BasePlatformController
    {
        public PositionsController(AppDbContext context) : base(context)
        {
        }

        // GET: Positions
        public async Task<IActionResult> Index()
        {
            return View(await _context.Positions.OrderBy(p => p.Name).ToListAsync());
        }

        // GET: Positions/Create
        public async Task<IActionResult> Create()
        {
            await LoadDynamicFields("Position");
            return View();
        }

        // POST: Positions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        // ИСПРАВЛЕНО: Принимаем IFormCollection вместо Dictionary
        public async Task<IActionResult> Create(Position position, IFormCollection form)
        {
            // ИСПРАВЛЕНО: Добавлен await и новый параметр "Position"
            await SaveDynamicProperties(position, form, "Position");

            if (ModelState.IsValid)
            {
                position.Id = Guid.NewGuid();
                _context.Add(position);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            
            await LoadDynamicFields("Position");
            return View(position);
        }

        // GET: Positions/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var position = await _context.Positions.FindAsync(id);
            if (position == null) return NotFound();

            await LoadDynamicFields("Position");
            return View(position);
        }

        // POST: Positions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        // ИСПРАВЛЕНО: Принимаем IFormCollection
        public async Task<IActionResult> Edit(Guid id, Position position, IFormCollection form)
        {
            if (id != position.Id) return NotFound();

            // ИСПРАВЛЕНО: Добавлен await и новый параметр "Position"
            await SaveDynamicProperties(position, form, "Position");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(position);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PositionExists(position.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            
            await LoadDynamicFields("Position");
            return View(position);
        }

        // GET: Positions/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var position = await _context.Positions.FirstOrDefaultAsync(m => m.Id == id);
            if (position == null) return NotFound();

            return View(position);
        }

        // POST: Positions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var position = await _context.Positions.FindAsync(id);
            if (position != null)
            {
                _context.Positions.Remove(position);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool PositionExists(Guid id)
        {
            return _context.Positions.Any(e => e.Id == id);
        }
    }
}