using MedicalBot.Data;
using MedicalBot.Entities.Company;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedicalWeb.Controllers
{
    public class PositionsController : Controller
    {
        private readonly AppDbContext _context;

        public PositionsController(AppDbContext context)
        {
            _context = context;
        }

        // Список всех должностей
        public async Task<IActionResult> Index()
        {
            var positions = await _context.Positions.ToListAsync();
            return View(positions);
        }

        // Создание (форма)
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Position position)
        {
            if (ModelState.IsValid)
            {
                position.Id = Guid.NewGuid();
                _context.Add(position);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(position);
        }

        // Удаление
        [HttpPost]
        public async Task<IActionResult> Delete(Guid id)
        {
            var position = await _context.Positions.FindAsync(id);
            if (position != null)
            {
                _context.Positions.Remove(position);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}