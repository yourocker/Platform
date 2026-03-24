using Core.Data;
using Core.Entities.Company;
using Microsoft.EntityFrameworkCore;

namespace CRM.Infrastructure
{
    public enum BookingCalendarDecorationKind
    {
        CompanyClosed,
        CompanyLunch,
        EmployeeAbsence
    }

    public sealed class BookingCalendarDecoration
    {
        public Guid? Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public DateTime Start { get; init; }
        public DateTime End { get; init; }
        public BookingCalendarDecorationKind Kind { get; init; }
        public bool IsFullDay { get; init; }
    }

    public interface IBookingCalendarDecorationService
    {
        Task<IReadOnlyList<BookingCalendarDecoration>> GetDecorationsAsync(
            DateTime rangeStart,
            DateTime rangeEnd,
            Guid? employeeId = null,
            CancellationToken cancellationToken = default);
    }

    public sealed class BookingCalendarDecorationService : IBookingCalendarDecorationService
    {
        private readonly AppDbContext _context;

        public BookingCalendarDecorationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<BookingCalendarDecoration>> GetDecorationsAsync(
            DateTime rangeStart,
            DateTime rangeEnd,
            Guid? employeeId = null,
            CancellationToken cancellationToken = default)
        {
            var effectiveStart = rangeStart;
            var effectiveEnd = rangeEnd > rangeStart ? rangeEnd : rangeStart.AddDays(1);
            var firstDate = effectiveStart.Date;
            var lastDateExclusive = effectiveEnd.TimeOfDay == TimeSpan.Zero
                ? effectiveEnd.Date
                : effectiveEnd.Date.AddDays(1);

            if (lastDateExclusive <= firstDate)
            {
                lastDateExclusive = firstDate.AddDays(1);
            }

            var workModes = await _context.CompanyWorkModes
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var holidayDates = await _context.CompanyHolidays
                .AsNoTracking()
                .Where(h => h.Date.Date >= firstDate && h.Date.Date < lastDateExclusive)
                .Select(h => h.Date.Date)
                .ToListAsync(cancellationToken);

            var holidaySet = holidayDates.ToHashSet();
            var decorations = new List<BookingCalendarDecoration>();

            for (var date = firstDate; date < lastDateExclusive; date = date.AddDays(1))
            {
                var dayMode = workModes.FirstOrDefault(m => m.DayOfWeek == date.DayOfWeek);
                var isHoliday = holidaySet.Contains(date);

                if (isHoliday || dayMode == null || dayMode.IsWeekend)
                {
                    decorations.Add(new BookingCalendarDecoration
                    {
                        Title = isHoliday ? "Праздничный/выходной день" : "Компания не работает",
                        Start = date,
                        End = date.AddDays(1),
                        Kind = BookingCalendarDecorationKind.CompanyClosed,
                        IsFullDay = true
                    });
                    continue;
                }

                AddDecoration(
                    decorations,
                    "Нерабочее время",
                    date,
                    date + dayMode.StartTime,
                    BookingCalendarDecorationKind.CompanyClosed);

                if (dayMode.LunchStartTime.HasValue && dayMode.LunchEndTime.HasValue)
                {
                    AddDecoration(
                        decorations,
                        "Обед",
                        date + dayMode.LunchStartTime.Value,
                        date + dayMode.LunchEndTime.Value,
                        BookingCalendarDecorationKind.CompanyLunch);
                }

                AddDecoration(
                    decorations,
                    "Нерабочее время",
                    date + dayMode.EndTime,
                    date.AddDays(1),
                    BookingCalendarDecorationKind.CompanyClosed);
            }

            if (employeeId.HasValue && employeeId.Value != Guid.Empty)
            {
                var absences = await _context.EmployeeSchedules
                    .AsNoTracking()
                    .Where(s =>
                        s.EmployeeId == employeeId.Value &&
                        s.IsAbsence &&
                        s.StartTime < effectiveEnd &&
                        s.EndTime > effectiveStart)
                    .OrderBy(s => s.StartTime)
                    .ToListAsync(cancellationToken);

                decorations.AddRange(absences.Select(absence => new BookingCalendarDecoration
                {
                    Id = absence.Id,
                    Title = "Отсутствие",
                    Start = absence.StartTime,
                    End = absence.EndTime,
                    Kind = BookingCalendarDecorationKind.EmployeeAbsence,
                    IsFullDay = absence.StartTime.TimeOfDay == TimeSpan.Zero &&
                                absence.EndTime == absence.StartTime.Date.AddDays(1)
                }));
            }

            return decorations;
        }

        private static void AddDecoration(
            ICollection<BookingCalendarDecoration> decorations,
            string title,
            DateTime start,
            DateTime end,
            BookingCalendarDecorationKind kind)
        {
            if (end <= start)
            {
                return;
            }

            decorations.Add(new BookingCalendarDecoration
            {
                Title = title,
                Start = start,
                End = end,
                Kind = kind
            });
        }
    }
}
