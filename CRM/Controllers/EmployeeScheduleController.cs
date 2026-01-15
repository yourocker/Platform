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
    public class EmployeeScheduleController : BasePlatformController
    {
        public EmployeeScheduleController(AppDbContext context, IWebHostEnvironment hostingEnvironment) 
            : base(context, hostingEnvironment)
        {
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
            
            var modes = await _context.CompanyWorkModes.Where(m => !m.IsWeekend).ToListAsync();
            if (modes.Any())
            {
                var start = modes.Min(m => m.StartTime).Subtract(TimeSpan.FromHours(1));
                var end = modes.Max(m => m.EndTime).Add(TimeSpan.FromHours(1));
                
                ViewBag.MinTime = (start < TimeSpan.Zero ? TimeSpan.Zero : start).ToString(@"hh\:mm\:ss");
                ViewBag.MaxTime = (end > TimeSpan.FromHours(23) ? "23:59:59" : end.ToString(@"hh\:mm\:ss"));
            }
            else
            {
                ViewBag.MinTime = "08:00:00";
                ViewBag.MaxTime = "20:00:00";
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployees(Guid? departmentId)
        {
            var query = _context.Employees.AsNoTracking().Where(e => !e.IsDismissed);
            if (departmentId.HasValue && departmentId.Value != Guid.Empty)
            {
                query = query.Where(e => _context.StaffAppointments.Any(sa => sa.EmployeeId == e.Id && sa.DepartmentId == departmentId.Value));
            }

            var employeesData = await query.Select(e => new { e.Id, e.FirstName, e.LastName, e.MiddleName }).ToListAsync();
            var result = employeesData.Select(e => new { id = e.Id, fullName = $"{e.LastName} {e.FirstName} {e.MiddleName}".Trim() }).OrderBy(x => x.fullName);
            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetEvents(Guid employeeId, DateTime start, DateTime end)
        {
            if (employeeId == Guid.Empty) return Json(new List<object>());

            var workModes = await _context.CompanyWorkModes.ToListAsync();
            var holidays = await _context.CompanyHolidays.Where(h => h.Date >= start && h.Date <= end).ToListAsync();
            var absences = await _context.EmployeeSchedules
                .Where(s => s.EmployeeId == employeeId && s.IsAbsence && s.StartTime < end && s.EndTime > start)
                .ToListAsync();

            var events = new List<object>();

            for (var dt = start.Date; dt <= end.Date; dt = dt.AddDays(1))
            {
                var dayMode = workModes.FirstOrDefault(m => m.DayOfWeek == dt.DayOfWeek);
                bool isHoliday = holidays.Any(h => h.Date.Date == dt.Date);
                string darkBg = "#adb5bd"; 

                if (isHoliday || dayMode == null || dayMode.IsWeekend)
                {
                    events.Add(new { display = "background", start = dt.ToString("yyyy-MM-dd"), end = dt.AddDays(1).ToString("yyyy-MM-dd"), backgroundColor = darkBg });
                }
                else
                {
                    events.Add(new { display = "background", start = dt.ToString("yyyy-MM-dd") + "T00:00:00", end = dt.ToString("yyyy-MM-dd") + "T" + dayMode.StartTime.ToString(@"hh\:mm"), backgroundColor = darkBg });
                    
                    if (dayMode.LunchStartTime.HasValue && dayMode.LunchEndTime.HasValue)
                    {
                        events.Add(new { display = "background", start = dt.ToString("yyyy-MM-dd") + "T" + dayMode.LunchStartTime.Value.ToString(@"hh\:mm"), end = dt.ToString("yyyy-MM-dd") + "T" + dayMode.LunchEndTime.Value.ToString(@"hh\:mm"), backgroundColor = "#dee2e6", title = "Обед" });
                    }

                    events.Add(new { display = "background", start = dt.ToString("yyyy-MM-dd") + "T" + dayMode.EndTime.ToString(@"hh\:mm"), end = dt.AddDays(1).ToString("yyyy-MM-dd") + "T00:00:00", backgroundColor = darkBg });
                }
            }

            foreach (var abs in absences)
            {
                events.Add(new { 
                    id = abs.Id, 
                    title = "Отсутствие", 
                    start = abs.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"), 
                    end = abs.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"), 
                    backgroundColor = "#dc3545", 
                    borderColor = "#a71d2a",
                    textColor = "#fff"
                });
            }
            return Json(events);
        }

        [HttpPost]
        public async Task<IActionResult> SaveAbsence(Guid employeeId, string start, string end)
        {
            // Парсим строку напрямую, чтобы избежать смещения TZ
            var absence = new EmployeeSchedule { 
                Id = Guid.NewGuid(), 
                EmployeeId = employeeId, 
                StartTime = DateTime.Parse(start), 
                EndTime = DateTime.Parse(end), 
                IsAbsence = true 
            };
            _context.EmployeeSchedules.Add(absence);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAbsence(Guid id, string start, string end)
        {
            var item = await _context.EmployeeSchedules.FindAsync(id);
            if (item == null) return Json(new { success = false });

            item.StartTime = DateTime.Parse(start);
            item.EndTime = DateTime.Parse(end);

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAbsence(Guid id)
        {
            var item = await _context.EmployeeSchedules.FindAsync(id);
            if (item != null) { _context.EmployeeSchedules.Remove(item); await _context.SaveChangesAsync(); }
            return Json(new { success = true });
        }
    }
}