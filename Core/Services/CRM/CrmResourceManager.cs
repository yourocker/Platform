using Core.Data;
using Core.Entities.Company;
using Core.Entities.CRM;
using Core.Entities.System;
using Core.Interfaces.CRM;
using Core.Interfaces.Platform;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Data;

namespace Core.Services.CRM
{
    public class CrmResourceManager : ICrmResourceManager
    {
        private readonly AppDbContext _context;
        private readonly IBookingPolicyService _bookingPolicyService;

        public CrmResourceManager(
            AppDbContext context,
            IBookingPolicyService bookingPolicyService)
        {
            _context = context;
            _bookingPolicyService = bookingPolicyService;
        }

        public async Task<(bool Success, bool IsOverbooking, string Message)> CheckAvailabilityAsync(
            Guid resourceId,
            DateTime start,
            DateTime end,
            Guid? performerEmployeeId = null,
            bool allowOutsideCompanyWorkHours = false,
            Guid? excludeBookingId = null)
        {
            var result = await CheckAvailabilityInternalAsync(
                resourceId,
                start,
                end,
                performerEmployeeId,
                excludeBookingId,
                allowOutsideCompanyWorkHours);
            return (result.Success, result.IsOverbooking, result.Message);
        }

        public async Task<CrmResourceBooking> BookResourceAsync(
            CrmResourceBooking booking,
            bool allowOutsideCompanyWorkHours = false)
        {
            ValidateBookingCore(booking);

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                var check = await CheckAvailabilityInternalAsync(
                    booking.ResourceId,
                    booking.StartTime,
                    booking.EndTime,
                    booking.PerformerEmployeeId.Value,
                    null,
                    allowOutsideCompanyWorkHours);
                if (!check.Success)
                {
                    throw new InvalidOperationException(check.Message);
                }

                booking.Id = booking.Id == Guid.Empty ? Guid.NewGuid() : booking.Id;
                booking.IsOverbooking = check.IsOverbooking;
                booking.CreatedAt = DateTime.UtcNow;
                EnsureBookingRelations(booking);

                _context.CrmResourceBookings.Add(booking);
                _context.OutboxEvents.Add(CreateBookingCreatedEvent(booking));

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return booking;
            });
        }

        public async Task<CrmResourceBooking> UpdateBookingAsync(
            CrmResourceBooking booking,
            bool allowOutsideCompanyWorkHours = false)
        {
            if (booking.Id == Guid.Empty)
            {
                throw new InvalidOperationException("Не указан идентификатор бронирования.");
            }

            ValidateBookingCore(booking);

            var strategy = _context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                var existing = await _context.CrmResourceBookings
                    .FirstOrDefaultAsync(b => b.Id == booking.Id);

                if (existing == null)
                {
                    throw new InvalidOperationException("Бронирование не найдено.");
                }

                var check = await CheckAvailabilityInternalAsync(
                    booking.ResourceId,
                    booking.StartTime,
                    booking.EndTime,
                    booking.PerformerEmployeeId.Value,
                    booking.Id,
                    allowOutsideCompanyWorkHours);
                if (!check.Success)
                {
                    throw new InvalidOperationException(check.Message);
                }

                existing.ResourceId = booking.ResourceId;
                existing.PerformerEmployeeId = booking.PerformerEmployeeId;
                existing.ServiceItemId = booking.ServiceItemId;
                existing.StatusId = booking.StatusId;
                existing.Title = booking.Title;
                existing.StartTime = booking.StartTime;
                existing.EndTime = booking.EndTime;
                existing.Amount = booking.Amount;
                existing.DiscountReason = booking.DiscountReason;
                existing.Comment = booking.Comment;
                existing.Properties = booking.Properties;
                existing.IsOverbooking = check.IsOverbooking;

                var updatedItems = new List<CrmResourceBookingItem>();
                foreach (var item in booking.BookingItems ?? new List<CrmResourceBookingItem>())
                {
                    updatedItems.Add(new CrmResourceBookingItem
                    {
                        Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
                        BookingId = existing.Id,
                        ServiceItemId = item.ServiceItemId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        CustomUnitPrice = item.CustomUnitPrice,
                        DiscountAmount = item.DiscountAmount,
                        LineTotal = item.LineTotal
                    });
                }

                var updatedContacts = new List<CrmResourceBookingContact>();
                foreach (var link in booking.BookingContacts ?? new List<CrmResourceBookingContact>())
                {
                    updatedContacts.Add(new CrmResourceBookingContact
                    {
                        BookingId = existing.Id,
                        ContactId = link.ContactId
                    });
                }

                // Avoid optimistic concurrency errors on child collections when replacing composition.
                await _context.CrmResourceBookingItems
                    .Where(i => i.BookingId == existing.Id)
                    .ExecuteDeleteAsync();

                await _context.CrmResourceBookingContacts
                    .Where(c => c.BookingId == existing.Id)
                    .ExecuteDeleteAsync();

                if (updatedItems.Count > 0)
                {
                    await _context.CrmResourceBookingItems.AddRangeAsync(updatedItems);
                }

                if (updatedContacts.Count > 0)
                {
                    await _context.CrmResourceBookingContacts.AddRangeAsync(updatedContacts);
                }

                existing.BookingItems = updatedItems;
                existing.BookingContacts = updatedContacts;

                _context.OutboxEvents.Add(CreateBookingUpdatedEvent(existing));

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return existing;
            });
        }

        public async Task<List<DateTime>> GetAvailableSlotsAsync(
            Guid resourceId,
            DateTime date,
            int durationMinutes = 15,
            Guid? performerEmployeeId = null)
        {
            if (durationMinutes < 5)
            {
                durationMinutes = 5;
            }

            var result = new List<DateTime>();

            var workMode = await _context.CompanyWorkModes
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.DayOfWeek == date.DayOfWeek);

            if (workMode == null || workMode.IsWeekend)
            {
                return result;
            }

            var slotStart = date.Date + workMode.StartTime;
            var dayEnd = date.Date + workMode.EndTime;

            while (slotStart.AddMinutes(durationMinutes) <= dayEnd)
            {
                var slotEnd = slotStart.AddMinutes(durationMinutes);
                var check = await CheckAvailabilityInternalAsync(resourceId, slotStart, slotEnd, performerEmployeeId, null);
                
                // GetAvailableSlots is used for regular working hours selection,
                // so we do not bypass company work-time checks here.

                if (check.Success)
                {
                    result.Add(slotStart);
                }

                slotStart = slotStart.AddMinutes(durationMinutes);
            }

            return result;
        }

        private async Task<AvailabilityResult> CheckAvailabilityInternalAsync(
            Guid resourceId,
            DateTime start,
            DateTime end,
            Guid? performerEmployeeId,
            Guid? excludeBookingId,
            bool allowOutsideCompanyWorkHours = false)
        {
            if (start >= end)
            {
                return AvailabilityResult.Fail("Некорректный интервал бронирования.");
            }

            if (start.Date != end.Date)
            {
                return AvailabilityResult.Fail("Бронирование должно находиться в пределах одного календарного дня.");
            }

            var resource = await _context.CrmResources
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == resourceId && r.IsActive);

            if (resource == null)
            {
                return AvailabilityResult.Fail("Ресурс не найден или отключен.");
            }

            if (!allowOutsideCompanyWorkHours)
            {
                var isHoliday = await _context.CompanyHolidays
                    .AsNoTracking()
                    .AnyAsync(h => h.Date.Date == start.Date);

                if (isHoliday)
                {
                    return AvailabilityResult.Fail("Выбранный день является праздничным/выходным.");
                }

                var workMode = await _context.CompanyWorkModes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.DayOfWeek == start.DayOfWeek);

                if (workMode == null || workMode.IsWeekend)
                {
                    return AvailabilityResult.Fail("Компания не работает в этот день.");
                }

                if (start.TimeOfDay < workMode.StartTime || end.TimeOfDay > workMode.EndTime)
                {
                    return AvailabilityResult.Fail(
                        $"Время вне рабочего диапазона компании ({workMode.StartTime:hh\\:mm} - {workMode.EndTime:hh\\:mm}).");
                }

                if (workMode.LunchStartTime.HasValue && workMode.LunchEndTime.HasValue)
                {
                    var lunchStart = start.Date + workMode.LunchStartTime.Value;
                    var lunchEnd = start.Date + workMode.LunchEndTime.Value;
                    if (start < lunchEnd && end > lunchStart)
                    {
                        return AvailabilityResult.Fail("Выбранный интервал пересекается с обеденным перерывом компании.");
                    }
                }
            }

            if (performerEmployeeId.HasValue)
            {
                var schedules = await _context.EmployeeSchedules
                    .AsNoTracking()
                    .Where(s => s.EmployeeId == performerEmployeeId.Value && start < s.EndTime && end > s.StartTime)
                    .ToListAsync();

                if (schedules.Any(s => s.IsAbsence))
                {
                    return AvailabilityResult.Fail("Сотрудник отсутствует (отпуск/больничный) в это время.");
                }

                var workShifts = schedules.Where(s => !s.IsAbsence).ToList();
                if (workShifts.Any() && !workShifts.Any(s => start >= s.StartTime && end <= s.EndTime))
                {
                    return AvailabilityResult.Fail("Время вне рабочей смены сотрудника.");
                }
            }

            var overlapCount = await _context.CrmResourceBookings
                .AsNoTracking()
                .CountAsync(b =>
                    b.ResourceId == resourceId &&
                    (!excludeBookingId.HasValue || b.Id != excludeBookingId.Value) &&
                    start < b.EndTime &&
                    end > b.StartTime);

            var policy = await _bookingPolicyService.GetEffectivePolicyAsync(resource);
            var maxParallel = policy.AllowOverbooking ? policy.MaxParallelBookings : 1;

            if (overlapCount >= maxParallel)
            {
                var message = policy.AllowOverbooking
                    ? $"Лимит параллельных записей достигнут (максимум: {maxParallel})."
                    : "На выбранное время уже есть запись.";
                return AvailabilityResult.Fail(message);
            }

            if (overlapCount > 0)
            {
                return AvailabilityResult.Ok(true, "Время доступно, запись будет создана как овербукинг.");
            }

            return AvailabilityResult.Ok(false, "Время доступно.");
        }

        private static OutboxEvent CreateBookingCreatedEvent(CrmResourceBooking booking)
        {
            var payload = new
            {
                BookingId = booking.Id,
                booking.PerformerEmployeeId,
                booking.CreatedByEmployeeId,
                booking.ResourceId,
                booking.ServiceItemId,
                booking.StatusId,
                booking.Title,
                booking.DealItemId,
                booking.StartTime,
                booking.EndTime,
                booking.IsOverbooking,
                booking.Amount,
                booking.DiscountReason,
                booking.Comment,
                booking.Properties,
                Items = booking.BookingItems?.Select(i => new
                {
                    i.ServiceItemId,
                    i.Quantity,
                    i.UnitPrice,
                    i.CustomUnitPrice,
                    i.DiscountAmount,
                    i.LineTotal
                }),
                Contacts = booking.BookingContacts?.Select(c => c.ContactId)
            };

            return new OutboxEvent
            {
                Id = Guid.NewGuid(),
                EventType = "RESOURCE_BOOKING_CREATED",
                Payload = JsonConvert.SerializeObject(payload),
                CreatedAt = DateTime.UtcNow
            };
        }

        private static OutboxEvent CreateBookingUpdatedEvent(CrmResourceBooking booking)
        {
            var payload = new
            {
                BookingId = booking.Id,
                booking.PerformerEmployeeId,
                booking.CreatedByEmployeeId,
                booking.ResourceId,
                booking.ServiceItemId,
                booking.StatusId,
                booking.Title,
                booking.StartTime,
                booking.EndTime,
                booking.IsOverbooking,
                booking.Amount,
                booking.DiscountReason,
                booking.Comment,
                booking.Properties,
                Items = booking.BookingItems?.Select(i => new
                {
                    i.ServiceItemId,
                    i.Quantity,
                    i.UnitPrice,
                    i.CustomUnitPrice,
                    i.DiscountAmount,
                    i.LineTotal
                }),
                Contacts = booking.BookingContacts?.Select(c => c.ContactId)
            };

            return new OutboxEvent
            {
                Id = Guid.NewGuid(),
                EventType = "RESOURCE_BOOKING_UPDATED",
                Payload = JsonConvert.SerializeObject(payload),
                CreatedAt = DateTime.UtcNow
            };
        }

        private static void ValidateBookingCore(CrmResourceBooking booking)
        {
            if (!booking.PerformerEmployeeId.HasValue || booking.PerformerEmployeeId == Guid.Empty)
            {
                throw new InvalidOperationException("Не указан исполнитель записи.");
            }

            if (!booking.CreatedByEmployeeId.HasValue || booking.CreatedByEmployeeId == Guid.Empty)
            {
                throw new InvalidOperationException("Не указан сотрудник, создавший запись.");
            }

            var hasPrimaryService = booking.ServiceItemId.HasValue && booking.ServiceItemId != Guid.Empty;
            var hasItems = booking.BookingItems != null && booking.BookingItems.Any();
            if (!hasPrimaryService && !hasItems)
            {
                throw new InvalidOperationException("Не указаны услуги/товары бронирования.");
            }
        }

        private static void EnsureBookingRelations(CrmResourceBooking booking)
        {
            if (booking.BookingItems != null)
            {
                foreach (var item in booking.BookingItems)
                {
                    item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
                    item.BookingId = booking.Id;
                }
            }

            if (booking.BookingContacts != null)
            {
                foreach (var link in booking.BookingContacts)
                {
                    link.BookingId = booking.Id;
                }
            }
        }

        private sealed record AvailabilityResult(bool Success, bool IsOverbooking, string Message)
        {
            public static AvailabilityResult Fail(string message) => new(false, false, message);

            public static AvailabilityResult Ok(bool isOverbooking, string message) => new(true, isOverbooking, message);
        }
    }
}
