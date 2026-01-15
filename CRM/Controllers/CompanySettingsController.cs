using Core.Data;
using Core.Entities.Company;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace CRM.Controllers
{
    public class CompanySettingsController : BasePlatformController
    {
        public CompanySettingsController(AppDbContext context, IWebHostEnvironment hostingEnvironment) 
            : base(context, hostingEnvironment)
        {
        }

        // Страница настройки режима работы и праздников
        public async Task<IActionResult> WorkMode()
        {
            var modes = await _context.CompanyWorkModes
                .OrderBy(x => x.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)x.DayOfWeek)
                .ToListAsync();

            // Если настроек нет, создаем дефолтные 7 дней
            if (!modes.Any())
            {
                var days = new[] { 
                    DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, 
                    DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday 
                };
                foreach (var day in days)
                {
                    _context.CompanyWorkModes.Add(new CompanyWorkMode
                    {
                        Id = Guid.NewGuid(),
                        DayOfWeek = day,
                        StartTime = new TimeSpan(9, 0, 0),
                        EndTime = new TimeSpan(18, 0, 0),
                        IsWeekend = (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday)
                    });
                }
                await _context.SaveChangesAsync();
                modes = await _context.CompanyWorkModes
                    .OrderBy(x => x.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)x.DayOfWeek)
                    .ToListAsync();
            }

            ViewBag.Holidays = await _context.CompanyHolidays.OrderBy(h => h.Date).ToListAsync();
            return View(modes);
        }

        [HttpPost]
        public async Task<IActionResult> SaveWorkMode(List<CompanyWorkMode> modes)
        {
            if (modes == null) return RedirectToAction(nameof(WorkMode));

            foreach (var m in modes)
            {
                var dbEntry = await _context.CompanyWorkModes.FindAsync(m.Id);
                if (dbEntry != null)
                {
                    dbEntry.StartTime = m.StartTime;
                    dbEntry.EndTime = m.EndTime;
                    dbEntry.IsWeekend = m.IsWeekend;
                    dbEntry.LunchStartTime = m.LunchStartTime;
                    dbEntry.LunchEndTime = m.LunchEndTime;
                }
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(WorkMode));
        }

        [HttpPost]
        public async Task<IActionResult> AddHoliday(DateTime date, string description)
        {
            if (date != default)
            {
                _context.CompanyHolidays.Add(new CompanyHoliday { 
                    Id = Guid.NewGuid(), 
                    Date = date, 
                    Description = description 
                });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(WorkMode));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteHoliday(Guid id)
        {
            var h = await _context.CompanyHolidays.FindAsync(id);
            if (h != null) _context.CompanyHolidays.Remove(h);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(WorkMode));
        }
    }
}