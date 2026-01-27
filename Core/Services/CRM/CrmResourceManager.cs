using Core.Data;
using Core.Entities.CRM;
using Core.Entities.Company;
using Core.Interfaces.CRM;
using Microsoft.EntityFrameworkCore;

namespace Core.Services.CRM
{
    public class CrmResourceManager : ICrmResourceManager
    {
        private readonly AppDbContext _context;

        public CrmResourceManager(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Success, bool IsOverbooking, string Message)> CheckAvailabilityAsync(Guid resourceId, DateTime start, DateTime end)
        {
            var resource = await _context.CrmResources.FindAsync(resourceId);
            if (resource == null) return (false, false, "Ресурс не найден");

            // 1. Проверка праздников (CompanyHoliday)
            var isHoliday = await _context.Set<CompanyHoliday>().AnyAsync(h => h.Date.Date == start.Date);
            if (isHoliday) return (false, false, "Выбранный день является праздничным/выходным");

            // 2. Проверка режима работы компании (CompanyWorkMode)
            var workMode = await _context.Set<CompanyWorkMode>().FirstOrDefaultAsync(w => w.DayOfWeek == start.DayOfWeek);
            if (workMode == null || workMode.IsWeekend) return (false, false, "Компания не работает в этот день");
            
            if (start.TimeOfDay < workMode.StartTime || end.TimeOfDay > workMode.EndTime)
                return (false, false, $"Время вне рабочего диапазона компании ({workMode.StartTime:hh\\:mm} - {workMode.EndTime:hh\\:mm})");

            // 3. Если ресурс привязан к сотруднику, проверяем его график (EmployeeSchedule)
            if (resource.EmployeeId.HasValue)
            {
                var schedules = await _context.Set<EmployeeSchedule>()
                    .Where(s => s.EmployeeId == resource.EmployeeId.Value && s.StartTime.Date == start.Date)
                    .ToListAsync();

                // Проверяем отсутствия (Absence)
                if (schedules.Any(s => s.IsAbsence && start < s.EndTime && end > s.StartTime))
                    return (false, false, "Сотрудник отсутствует (отпуск/больничный) в это время");
                
                // Проверяем, попадает ли в рабочую смену (если смены заведены)
                var workShifts = schedules.Where(s => !s.IsAbsence).ToList();
                if (workShifts.Any() && !workShifts.Any(s => start >= s.StartTime && end <= s.EndTime))
                    return (false, false, "Время вне рабочей смены сотрудника");
            }

            // 4. Проверка существующих броней (CrmResourceBooking) для Овербукинга
            var existingBooking = await _context.CrmResourceBookings
                .AnyAsync(b => b.ResourceId == resourceId && start < b.EndTime && end > b.StartTime);

            if (existingBooking)
            {
                return (true, true, "Внимание, на это время уже есть запись! (Овербукинг)");
            }

            return (true, false, "Время доступно");
        }

        public async Task<CrmResourceBooking> BookResourceAsync(CrmResourceBooking booking)
        {
            var check = await CheckAvailabilityAsync(booking.ResourceId, booking.StartTime, booking.EndTime);
            
            if (!check.Success) throw new Exception(check.Message);

            // Фиксируем факт овербукинга, чтобы выделить в UI
            booking.IsOverbooking = check.IsOverbooking;

            _context.CrmResourceBookings.Add(booking);
            await _context.SaveChangesAsync();
            return booking;
        }

        public async Task<List<DateTime>> GetAvailableSlotsAsync(Guid resourceId, DateTime date, int durationMinutes = 15)
        {
            // Здесь будет логика генерации 15-минутных слотов
            // для UI календаря с учетом CheckAvailabilityAsync
            return new List<DateTime>(); 
        }
    }
}