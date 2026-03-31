using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Core.Data;
using Core.Entities.CRM;
using Core.Entities.Platform;
using Core.Entities.System;
using Core.Interfaces.CRM;
using Core.Interfaces.Platform;
using CRM.Infrastructure;
using CRM.ViewModels.Schedule;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace CRM.Controllers
{
    public class ScheduleController : BasePlatformController
    {
        private const string BookingEntityCode = "ResourceBooking";
        private const string ScheduleViewCode = "ScheduleCalendar";
        private const string DefaultNewStatusName = "Новая запись";
        private const string DefaultIntermediateStatusName = "В работе";
        private const string DefaultSuccessStatusName = "Успешно";
        private const string DefaultFailedStatusName = "Неуспешно";

        private readonly ICrmResourceManager _resourceManager;
        private readonly IBookingPolicyService _bookingPolicyService;
        private readonly IBookingCalendarDecorationService _calendarDecorationService;
        private readonly IEntityTimelineService _timelineService;

        public ScheduleController(
            AppDbContext context,
            IWebHostEnvironment hostingEnvironment,
            ICrmResourceManager resourceManager,
            IBookingPolicyService bookingPolicyService,
            IBookingCalendarDecorationService calendarDecorationService,
            IEntityTimelineService timelineService)
            : base(context, hostingEnvironment)
        {
            _resourceManager = resourceManager;
            _bookingPolicyService = bookingPolicyService;
            _calendarDecorationService = calendarDecorationService;
            _timelineService = timelineService;
        }

        public async Task<IActionResult> Index()
        {
            await EnsureDefaultBookingStatusesAsync();

            ViewBag.Resources = await _context.CrmResources
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .ToListAsync();

            ViewBag.Employees = await _context.Employees
                .AsNoTracking()
                .Where(e => !e.IsDismissed)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();

            var services = await _context.ServiceItems
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync();

            ViewBag.Services = services;
            ViewBag.ServicesJson = JsonSerializer.Serialize(services.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                price = s.Price
            }));

            var contacts = await _context.Contacts
                .AsNoTracking()
                .OrderBy(c => c.FullName)
                .Select(c => new
                {
                    c.Id,
                    c.FullName
                })
                .ToListAsync();
            ViewBag.ContactsJson = JsonSerializer.Serialize(contacts.Select(c => new
            {
                id = c.Id,
                name = c.FullName
            }));

            var statuses = await _context.BookingStatuses
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.Category)
                .ThenBy(s => s.SortOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();
            ViewBag.BookingStatusesJson = JsonSerializer.Serialize(statuses.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                category = s.Category.ToString(),
                sortOrder = s.SortOrder,
                color = GetColorByStatusCategory(s.Category)
            }));

            var bookingDefinition = await _context.AppDefinitions
                .AsNoTracking()
                .Include(d => d.Fields)
                .FirstOrDefaultAsync(d => d.EntityCode == BookingEntityCode);

            var customFields = bookingDefinition?.Fields
                .Where(f => !f.IsDeleted && !string.Equals(f.SystemName, "Name", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.SortOrder)
                .ToList() ?? new List<AppFieldDefinition>();

            var lookupData = await BuildEntityLinkLookupDataAsync(customFields);
            var bookingQuickCreatableEntityCodes = await BuildQuickCreatableEntityCodeSetAsync(customFields);

            ViewBag.BookingCustomFieldsJson = JsonSerializer.Serialize(customFields.Select(f => new
            {
                f.Label,
                f.SystemName,
                DataType = f.DataType.ToString(),
                f.IsRequired,
                f.IsArray,
                f.TargetEntityCode,
                CanQuickCreate = CanQuickCreateEntity(f.TargetEntityCode, bookingQuickCreatableEntityCodes),
                ModalCreateUrl = BuildModalCreateUrl(f.TargetEntityCode, bookingQuickCreatableEntityCodes)
            }));

            ViewBag.BookingLookupDataJson = JsonSerializer.Serialize(
                lookupData.ToDictionary(
                    x => x.Key,
                    x => x.Value.Select(v => new { v.Value, v.Text })));

            ViewBag.BookingEntityCode = BookingEntityCode;

            var bookingPolicy = await _context.BookingPolicySettings
                .AsNoTracking()
                .FirstOrDefaultAsync();
            ViewBag.AllowItemPriceChange = bookingPolicy?.AllowManualItemPriceChange == true;

            var currentEmployeeId = GetCurrentEmployeeId();
            ViewBag.CurrentEmployeeId = currentEmployeeId?.ToString();
            ViewBag.CurrentEmployeeName = await GetEmployeeDisplayNameAsync(currentEmployeeId);

            var modes = await _context.CompanyWorkModes.Where(m => !m.IsWeekend).ToListAsync();
            if (modes.Any())
            {
                var start = modes.Min(m => m.StartTime);
                var end = modes.Max(m => m.EndTime);

                ViewBag.MinTime = start.ToString(@"hh\:mm\:ss");
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
        public async Task<IActionResult> GetEvents(DateTime start, DateTime end)
        {
            var filters = ReadFiltersFromQuery();

            var query = _context.CrmResourceBookings
                .AsNoTracking()
                .Include(b => b.Resource)
                .Include(b => b.PerformerEmployee)
                .Include(b => b.CreatedByEmployee)
                .Include(b => b.Status)
                .Include(b => b.ServiceItem)
                .Include(b => b.BookingItems)
                    .ThenInclude(i => i.ServiceItem)
                .Include(b => b.BookingContacts)
                    .ThenInclude(c => c.Contact)
                .Where(b => b.StartTime < end && b.EndTime > start);

            if (filters.TryGetValue("f_resourceId", out var resourceIdText) &&
                Guid.TryParse(resourceIdText, out var resourceId) &&
                resourceId != Guid.Empty)
            {
                query = query.Where(b => b.ResourceId == resourceId);
            }

            if (filters.TryGetValue("f_performerEmployeeId", out var performerText) &&
                Guid.TryParse(performerText, out var performerId) &&
                performerId != Guid.Empty)
            {
                query = query.Where(b => b.PerformerEmployeeId == performerId);
            }

            if (filters.TryGetValue("f_createdByEmployeeId", out var createdByText) &&
                Guid.TryParse(createdByText, out var createdById) &&
                createdById != Guid.Empty)
            {
                query = query.Where(b => b.CreatedByEmployeeId == createdById);
            }

            if (filters.TryGetValue("f_serviceItemId", out var serviceText) &&
                Guid.TryParse(serviceText, out var serviceId) &&
                serviceId != Guid.Empty)
            {
                query = query.Where(b =>
                    b.ServiceItemId == serviceId ||
                    b.BookingItems.Any(i => i.ServiceItemId == serviceId));
            }

            if (filters.TryGetValue("f_statusId", out var statusText) &&
                Guid.TryParse(statusText, out var statusId) &&
                statusId != Guid.Empty)
            {
                query = query.Where(b => b.StatusId == statusId);
            }

            if (filters.TryGetValue("f_contactId", out var contactText) &&
                Guid.TryParse(contactText, out var contactId) &&
                contactId != Guid.Empty)
            {
                query = query.Where(b => b.BookingContacts.Any(c => c.ContactId == contactId));
            }

            if (filters.TryGetValue("f_startFrom", out var startFromText) && DateTime.TryParse(startFromText, out var startFrom))
            {
                query = query.Where(b => b.StartTime >= startFrom.Date);
            }

            if (filters.TryGetValue("f_startTo", out var startToText) && DateTime.TryParse(startToText, out var startTo))
            {
                var endOfDay = startTo.Date.AddDays(1);
                query = query.Where(b => b.StartTime < endOfDay);
            }

            if (filters.TryGetValue("f_search", out var search) && !string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                query = query.Where(b =>
                    (b.Title != null && EF.Functions.ILike(b.Title, $"%{search}%")) ||
                    EF.Functions.ILike(b.Resource.Name, $"%{search}%") ||
                    (b.ServiceItem != null && EF.Functions.ILike(b.ServiceItem.Name, $"%{search}%")) ||
                    b.BookingItems.Any(i => EF.Functions.ILike(i.ServiceItem.Name, $"%{search}%")) ||
                    b.BookingContacts.Any(c => EF.Functions.ILike(c.Contact.FullName, $"%{search}%")) ||
                    (b.Status != null && EF.Functions.ILike(b.Status.Name, $"%{search}%")) ||
                    (b.PerformerEmployee != null &&
                     EF.Functions.ILike(b.PerformerEmployee.LastName + " " + b.PerformerEmployee.FirstName, $"%{search}%")) ||
                    (b.Comment != null && EF.Functions.ILike(b.Comment, $"%{search}%")) ||
                    (b.Properties != null && EF.Functions.ILike(b.Properties, $"%{search}%")));
            }

            foreach (var filter in filters.Where(x => x.Key.StartsWith("f_dyn_", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrWhiteSpace(filter.Value))
                {
                    continue;
                }

                var fieldName = filter.Key["f_dyn_".Length..];
                query = query.Where(b =>
                    b.Properties != null &&
                    EF.Functions.ILike(b.Properties, $"%\"{fieldName}\":%{filter.Value}%"));
            }

            var bookings = await query
                .OrderBy(b => b.StartTime)
                .ToListAsync();

            var events = bookings
                .Select(b =>
                {
                    var serviceList = b.BookingItems.Any()
                        ? b.BookingItems
                            .OrderBy(i => i.ServiceItem.Name)
                            .Select(i => $"{i.ServiceItem.Name} x{i.Quantity:0.##}")
                            .ToList()
                        : new List<string> { b.ServiceItem?.Name ?? "Без услуги" };

                    var performerName = b.PerformerEmployee?.FullName ?? "Без исполнителя";
                    var primaryServiceName = b.ServiceItem?.Name ??
                                             b.BookingItems
                                                 .Select(i => i.ServiceItem.Name)
                                                 .OrderBy(name => name)
                                                 .FirstOrDefault() ??
                                             "Без услуги";
                    var resolvedTitle = BuildBookingTitle(b.Title, performerName, b.Resource?.Name, primaryServiceName);
                    var backgroundColor = b.Status != null
                        ? GetColorByStatusCategory(b.Status.Category)
                        : (b.IsOverbooking ? "#f59f00" : "#198754");
                    var borderColor = b.Status != null
                        ? GetBorderByStatusCategory(b.Status.Category)
                        : (b.IsOverbooking ? "#d97706" : "#157347");

                    return new
                    {
                        id = b.Id,
                        title = resolvedTitle,
                        start = b.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                        end = b.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                        backgroundColor,
                        borderColor,
                        textColor = "#ffffff",
                        extendedProps = new
                        {
                            title = resolvedTitle,
                            performerEmployeeId = b.PerformerEmployeeId,
                            resource = b.Resource.Name,
                            services = serviceList,
                            contacts = b.BookingContacts.Select(c => c.Contact.FullName).OrderBy(x => x).ToList(),
                            statusId = b.StatusId,
                            statusName = b.Status?.Name,
                            performer = b.PerformerEmployee?.FullName ?? "Не указано",
                            createdBy = b.CreatedByEmployee?.FullName ?? "Не указано",
                            amount = b.Amount,
                            comment = b.Comment
                        }
                    };
                })
                .ToList();

            return Json(events);
        }

        [HttpGet]
        public async Task<IActionResult> GetCalendarDecorations(DateTime start, DateTime end)
        {
            var filters = ReadFiltersFromQuery();
            var performerEmployeeId = filters.TryGetValue("f_performerEmployeeId", out var performerText)
                ? ParseOptionalGuid(performerText)
                : null;

            var decorations = await _calendarDecorationService.GetDecorationsAsync(start, end, performerEmployeeId);
            var payload = decorations
                .Select(MapDecorationToScheduleEvent)
                .ToList();

            return Json(payload);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckSlotAvailability(
            Guid resourceId,
            Guid performerEmployeeId,
            DateTime start,
            DateTime end,
            Guid? bookingId = null,
            string? createSource = null)
        {
            var preview = await EvaluateSlotAvailabilityAsync(
                resourceId,
                performerEmployeeId,
                start,
                end,
                bookingId,
                createSource);

            return Json(new
            {
                success = true,
                canSave = preview.CanSave,
                requiresConfirmation = preview.RequiresConfirmation,
                isOverbooking = preview.IsOverbooking,
                message = preview.Message
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBooking(IFormCollection form)
        {
            var currentEmployeeId = GetCurrentEmployeeId();
            if (!currentEmployeeId.HasValue)
            {
                return Json(new { success = false, message = "Не удалось определить пользователя текущей сессии." });
            }

            if (!Guid.TryParse(form["resourceId"], out var resourceId) || resourceId == Guid.Empty)
            {
                return Json(new { success = false, message = "Выберите ресурс." });
            }

            if (!Guid.TryParse(form["performerEmployeeId"], out var performerEmployeeId) || performerEmployeeId == Guid.Empty)
            {
                return Json(new { success = false, message = "Выберите исполнителя." });
            }

            if (!DateTime.TryParse(form["start"], out var start) || !DateTime.TryParse(form["end"], out var end))
            {
                return Json(new { success = false, message = "Некорректное время записи." });
            }

            var createSource = form["createSource"].ToString().Trim();
            var allowOutOfHours = ParseBoolean(form["allowOutOfHours"]);
            var isOutsideCompanyWorkHours = await IsOutsideCompanyWorkHoursAsync(start, end);
            if (isOutsideCompanyWorkHours)
            {
                if (!string.Equals(createSource, "button", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = "Запись вне рабочего времени можно создать только через кнопку \"Добавить запись\"." });
                }

                if (!allowOutOfHours)
                {
                    return Json(new { success = false, message = "Подтвердите создание записи вне рабочего времени." });
                }
            }

            var statusId = ParseOptionalGuid(form["statusId"]);
            if (!statusId.HasValue)
            {
                return Json(new { success = false, message = "Выберите статус записи." });
            }

            var status = await _context.BookingStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == statusId.Value && s.IsActive);
            if (status == null)
            {
                return Json(new { success = false, message = "Выбранный статус недоступен." });
            }

            var bookingItemsBuildResult = await BuildBookingItemsFromFormAsync(form);
            if (!bookingItemsBuildResult.Success)
            {
                return Json(new { success = false, message = bookingItemsBuildResult.Message });
            }

            var bookingItems = bookingItemsBuildResult.Items;
            var amount = bookingItemsBuildResult.Amount;
            var hasDiscount = bookingItems.Any(i => i.DiscountAmount > 0);
            var discountReason = string.IsNullOrWhiteSpace(form["discountReason"])
                ? null
                : form["discountReason"].ToString().Trim();
            if (hasDiscount && string.IsNullOrWhiteSpace(discountReason))
            {
                return Json(new { success = false, message = "Укажите обоснование скидки." });
            }

            var contactIds = ParseGuidList(form["contactIds"]);
            if (contactIds.Any())
            {
                var existingContactIds = await _context.Contacts
                    .AsNoTracking()
                    .Where(c => contactIds.Contains(c.Id))
                    .Select(c => c.Id)
                    .ToListAsync();

                if (existingContactIds.Count != contactIds.Count)
                {
                    return Json(new { success = false, message = "Часть выбранных контактов не найдена." });
                }

                contactIds = existingContactIds;
            }

            var title = await ResolveBookingTitleAsync(
                form["title"],
                performerEmployeeId,
                resourceId,
                bookingItems.First().ServiceItemId);

            var bookingContacts = contactIds
                .Select(contactId => new CrmResourceBookingContact
                {
                    BookingId = Guid.Empty,
                    ContactId = contactId
                })
                .ToList();

            var booking = new CrmResourceBooking
            {
                Id = Guid.NewGuid(),
                ResourceId = resourceId,
                PerformerEmployeeId = performerEmployeeId,
                CreatedByEmployeeId = currentEmployeeId.Value,
                ServiceItemId = bookingItems.First().ServiceItemId,
                StatusId = statusId,
                Title = title,
                StartTime = start,
                EndTime = end,
                Amount = amount,
                DiscountReason = hasDiscount ? discountReason : null,
                Comment = string.IsNullOrWhiteSpace(form["comment"]) ? null : form["comment"].ToString().Trim(),
                BookingItems = bookingItems,
                BookingContacts = bookingContacts
            };

            await SaveDynamicProperties(booking, form, BookingEntityCode);
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault() ?? "Проверьте заполнение полей.";

                return Json(new { success = false, message = firstError });
            }

            try
            {
                FinalizeDynamicFilePaths(booking, BookingEntityCode, booking.Id.ToString());
                await _resourceManager.BookResourceAsync(booking, isOutsideCompanyWorkHours && allowOutOfHours);
                await LogBookingCreatedAsync(booking, currentEmployeeId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBooking(Guid id)
        {
            var booking = await _context.CrmResourceBookings
                .AsNoTracking()
                .Include(b => b.Resource)
                .Include(b => b.PerformerEmployee)
                .Include(b => b.CreatedByEmployee)
                .Include(b => b.BookingItems)
                    .ThenInclude(i => i.ServiceItem)
                .Include(b => b.BookingContacts)
                    .ThenInclude(c => c.Contact)
                .Include(b => b.Status)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return Json(new { success = false, message = "Бронирование не найдено." });
            }

            return Json(new
            {
                success = true,
                booking = new
                {
                    id = booking.Id,
                    resourceId = booking.ResourceId,
                    performerEmployeeId = booking.PerformerEmployeeId,
                    statusId = booking.StatusId,
                    start = booking.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = booking.EndTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    title = BuildBookingTitle(
                        booking.Title,
                        booking.PerformerEmployee?.FullName,
                        booking.Resource?.Name,
                        booking.ServiceItem?.Name ??
                        booking.BookingItems
                            .Select(i => i.ServiceItem.Name)
                            .OrderBy(name => name)
                            .FirstOrDefault()),
                    createdAt = booking.CreatedAt.ToString("O"),
                    amount = booking.Amount,
                    discountReason = booking.DiscountReason,
                    comment = booking.Comment,
                    resourceName = booking.Resource?.Name,
                    performerName = booking.PerformerEmployee?.FullName,
                    createdByName = booking.CreatedByEmployee?.FullName,
                    statusName = booking.Status?.Name,
                    statusCategory = booking.Status?.Category.ToString(),
                    primaryServiceItemId = booking.ServiceItemId,
                    properties = booking.Properties,
                    services = booking.BookingItems
                        .OrderBy(i => i.ServiceItem.Name)
                        .Select(i => new
                        {
                            serviceItemId = i.ServiceItemId,
                            name = i.ServiceItem.Name,
                            quantity = i.Quantity,
                            unitPrice = i.UnitPrice,
                            customUnitPrice = i.CustomUnitPrice,
                            discountAmount = i.DiscountAmount,
                            lineTotal = i.LineTotal
                        })
                        .ToList(),
                    contacts = booking.BookingContacts
                        .OrderBy(c => c.Contact.FullName)
                        .Select(c => new
                        {
                            contactId = c.ContactId,
                            name = c.Contact.FullName
                        })
                        .ToList()
                }
            });
        }

        public async Task<IActionResult> Details(Guid id)
        {
            ViewData["ModalSize"] = "xl";

            var booking = await _context.CrmResourceBookings
                .AsNoTracking()
                .Include(b => b.Resource)
                .Include(b => b.PerformerEmployee)
                .Include(b => b.CreatedByEmployee)
                .Include(b => b.BookingItems)
                    .ThenInclude(i => i.ServiceItem)
                .Include(b => b.BookingContacts)
                    .ThenInclude(c => c.Contact)
                .Include(b => b.Status)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (booking == null)
            {
                return NotFound();
            }

            var bookingDefinition = await _context.AppDefinitions
                .AsNoTracking()
                .Include(d => d.Fields)
                .FirstOrDefaultAsync(d => d.EntityCode == BookingEntityCode);

            var customFields = bookingDefinition?.Fields
                .Where(f => !f.IsDeleted && !string.Equals(f.SystemName, "Name", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.SortOrder)
                .ToList() ?? new List<AppFieldDefinition>();

            var lookupData = await BuildEntityLinkLookupDataAsync(customFields);
            var timelineEvents = await _timelineService.GetEventsAsync(booking.Id, BookingEntityCode);

            var model = new BookingDetailsViewModel
            {
                Booking = booking,
                CustomFields = BuildBookingDetailsCustomFields(booking, customFields, lookupData),
                TimelineEvents = timelineEvents
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBooking(IFormCollection form)
        {
            var currentEmployeeId = GetCurrentEmployeeId();
            if (!currentEmployeeId.HasValue)
            {
                return Json(new { success = false, message = "Не удалось определить пользователя текущей сессии." });
            }

            if (!Guid.TryParse(form["bookingId"], out var bookingId) || bookingId == Guid.Empty)
            {
                return Json(new { success = false, message = "Не указан идентификатор записи." });
            }

            var existing = await _context.CrmResourceBookings
                .AsNoTracking()
                .Include(b => b.BookingItems)
                .Include(b => b.BookingContacts)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
            if (existing == null)
            {
                return Json(new { success = false, message = "Бронирование не найдено." });
            }

            if (!Guid.TryParse(form["resourceId"], out var resourceId) || resourceId == Guid.Empty)
            {
                return Json(new { success = false, message = "Выберите ресурс." });
            }

            if (!Guid.TryParse(form["performerEmployeeId"], out var performerEmployeeId) || performerEmployeeId == Guid.Empty)
            {
                return Json(new { success = false, message = "Выберите исполнителя." });
            }

            if (!DateTime.TryParse(form["start"], out var start) || !DateTime.TryParse(form["end"], out var end))
            {
                return Json(new { success = false, message = "Некорректное время записи." });
            }

            var allowOutOfHours = ParseBoolean(form["allowOutOfHours"]);
            var isOutsideCompanyWorkHours = await IsOutsideCompanyWorkHoursAsync(start, end);
            var wasOutsideCompanyWorkHours = await IsOutsideCompanyWorkHoursAsync(existing.StartTime, existing.EndTime);
            var canBypassCompanyWorkHours = isOutsideCompanyWorkHours && (allowOutOfHours || wasOutsideCompanyWorkHours);
            if (isOutsideCompanyWorkHours && !canBypassCompanyWorkHours)
            {
                return Json(new { success = false, message = "Подтвердите сохранение записи вне рабочего времени." });
            }

            var statusId = ParseOptionalGuid(form["statusId"]);
            if (!statusId.HasValue)
            {
                return Json(new { success = false, message = "Выберите статус записи." });
            }

            var status = await _context.BookingStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == statusId.Value && s.IsActive);
            if (status == null)
            {
                return Json(new { success = false, message = "Выбранный статус недоступен." });
            }

            var bookingItemsBuildResult = await BuildBookingItemsFromFormAsync(form);
            if (!bookingItemsBuildResult.Success)
            {
                return Json(new { success = false, message = bookingItemsBuildResult.Message });
            }

            var bookingItems = bookingItemsBuildResult.Items;
            foreach (var item in bookingItems)
            {
                item.BookingId = bookingId;
            }

            var amount = bookingItemsBuildResult.Amount;
            var hasDiscount = bookingItems.Any(i => i.DiscountAmount > 0);
            var discountReason = string.IsNullOrWhiteSpace(form["discountReason"])
                ? null
                : form["discountReason"].ToString().Trim();
            if (hasDiscount && string.IsNullOrWhiteSpace(discountReason))
            {
                return Json(new { success = false, message = "Укажите обоснование скидки." });
            }

            var contactIds = ParseGuidList(form["contactIds"]);
            if (contactIds.Any())
            {
                var existingContactIds = await _context.Contacts
                    .AsNoTracking()
                    .Where(c => contactIds.Contains(c.Id))
                    .Select(c => c.Id)
                    .ToListAsync();

                if (existingContactIds.Count != contactIds.Count)
                {
                    return Json(new { success = false, message = "Часть выбранных контактов не найдена." });
                }

                contactIds = existingContactIds;
            }

            var title = await ResolveBookingTitleAsync(
                form["title"],
                performerEmployeeId,
                resourceId,
                bookingItems.First().ServiceItemId);

            var bookingContacts = contactIds
                .Select(contactId => new CrmResourceBookingContact
                {
                    BookingId = bookingId,
                    ContactId = contactId
                })
                .ToList();

            var booking = new CrmResourceBooking
            {
                Id = bookingId,
                ResourceId = resourceId,
                PerformerEmployeeId = performerEmployeeId,
                CreatedByEmployeeId = existing.CreatedByEmployeeId ?? currentEmployeeId.Value,
                ServiceItemId = bookingItems.First().ServiceItemId,
                StatusId = statusId,
                Title = title,
                StartTime = start,
                EndTime = end,
                Amount = amount,
                DiscountReason = hasDiscount ? discountReason : null,
                Comment = string.IsNullOrWhiteSpace(form["comment"]) ? null : form["comment"].ToString().Trim(),
                Properties = existing.Properties,
                BookingItems = bookingItems,
                BookingContacts = bookingContacts
            };

            await SaveDynamicProperties(booking, form, BookingEntityCode);
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault() ?? "Проверьте заполнение полей.";

                return Json(new { success = false, message = firstError });
            }

            try
            {
                FinalizeDynamicFilePaths(booking, BookingEntityCode, booking.Id.ToString());
                await _resourceManager.UpdateBookingAsync(booking, canBypassCompanyWorkHours);
                await LogBookingUpdatedAsync(existing, booking, currentEmployeeId, "Запись обновлена");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleBooking(Guid bookingId, DateTime start, DateTime end, bool allowOutOfHours = false)
        {
            var currentEmployeeId = GetCurrentEmployeeId();
            if (!currentEmployeeId.HasValue)
            {
                return Json(new { success = false, message = "Не удалось определить пользователя текущей сессии." });
            }

            if (bookingId == Guid.Empty)
            {
                return Json(new { success = false, message = "Не указан идентификатор записи." });
            }

            var existing = await _context.CrmResourceBookings
                .AsNoTracking()
                .Include(b => b.BookingItems)
                .Include(b => b.BookingContacts)
                .FirstOrDefaultAsync(b => b.Id == bookingId);
            if (existing == null)
            {
                return Json(new { success = false, message = "Бронирование не найдено." });
            }

            if (start >= end)
            {
                return Json(new { success = false, message = "Время окончания должно быть позже времени начала." });
            }

            var isOutsideCompanyWorkHours = await IsOutsideCompanyWorkHoursAsync(start, end);
            var wasOutsideCompanyWorkHours = await IsOutsideCompanyWorkHoursAsync(existing.StartTime, existing.EndTime);
            var canBypassCompanyWorkHours = isOutsideCompanyWorkHours && (allowOutOfHours || wasOutsideCompanyWorkHours);
            if (isOutsideCompanyWorkHours && !canBypassCompanyWorkHours)
            {
                return Json(new { success = false, message = "Подтвердите сохранение записи вне рабочего времени." });
            }

            var booking = new CrmResourceBooking
            {
                Id = existing.Id,
                ResourceId = existing.ResourceId,
                PerformerEmployeeId = existing.PerformerEmployeeId,
                CreatedByEmployeeId = existing.CreatedByEmployeeId ?? currentEmployeeId.Value,
                ServiceItemId = existing.ServiceItemId,
                StatusId = existing.StatusId,
                Title = existing.Title,
                StartTime = start,
                EndTime = end,
                Amount = existing.Amount,
                DiscountReason = existing.DiscountReason,
                Comment = existing.Comment,
                Properties = existing.Properties,
                BookingItems = existing.BookingItems
                    .Select(item => new CrmResourceBookingItem
                    {
                        Id = item.Id,
                        BookingId = existing.Id,
                        ServiceItemId = item.ServiceItemId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        CustomUnitPrice = item.CustomUnitPrice,
                        DiscountAmount = item.DiscountAmount,
                        LineTotal = item.LineTotal
                    })
                    .ToList(),
                BookingContacts = existing.BookingContacts
                    .Select(link => new CrmResourceBookingContact
                    {
                        BookingId = existing.Id,
                        ContactId = link.ContactId
                    })
                    .ToList()
            };

            try
            {
                await _resourceManager.UpdateBookingAsync(booking, canBypassCompanyWorkHours);
                await LogBookingUpdatedAsync(existing, booking, currentEmployeeId, "Время записи изменено");
                return Json(new
                {
                    success = true,
                    start = booking.StartTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = booking.EndTime.ToString("yyyy-MM-ddTHH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuickContact(IFormCollection form)
        {
            var lastName = (form["lastName"].ToString() ?? string.Empty).Trim();
            var firstName = (form["firstName"].ToString() ?? string.Empty).Trim();
            var middleName = string.IsNullOrWhiteSpace(form["middleName"])
                ? null
                : form["middleName"].ToString().Trim();

            if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName))
            {
                return Json(new { success = false, message = "Укажите фамилию и имя контакта." });
            }

            var contact = new Contact
            {
                Id = Guid.NewGuid(),
                EntityCode = "Contact",
                LastName = lastName,
                FirstName = firstName,
                MiddleName = middleName,
                CreatedAt = DateTime.UtcNow
            };
            contact.RecalculateFullName();
            contact.Name = contact.FullName;

            var phones = form["phones"]
                .Select(p => p?.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var phone in phones)
            {
                contact.Phones.Add(new ContactPhone
                {
                    ContactId = contact.Id,
                    Number = phone!
                });
            }

            var emails = form["emails"]
                .Select(e => e?.Trim())
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var email in emails)
            {
                contact.Emails.Add(new ContactEmail
                {
                    ContactId = contact.Id,
                    Email = email!
                });
            }

            await SaveDynamicProperties(contact, form, "Contact");
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault() ?? "Проверьте заполнение полей контакта.";

                return Json(new { success = false, message = firstError });
            }

            _context.Contacts.Add(contact);
            await _context.SaveChangesAsync();

            await _timelineService.LogEventAsync(
                contact.Id,
                "Contact",
                CrmEventType.System,
                "Контакт создан",
                $"Контакт создан из формы бронирования: \"{contact.FullName}\".",
                GetCurrentEmployeeId());

            return Json(new
            {
                success = true,
                id = contact.Id,
                name = contact.FullName
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBooking(Guid id)
        {
            var booking = await _context.CrmResourceBookings
                .AsNoTracking()
                .Include(b => b.BookingItems)
                .Include(b => b.BookingContacts)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (booking == null)
            {
                return Json(new { success = false, message = "Бронирование не найдено." });
            }

            _context.CrmResourceBookings.Remove(booking);
            await _context.SaveChangesAsync();

            await LogBookingDeletedAsync(booking, GetCurrentEmployeeId());

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetFilterPresets()
        {
            var currentUserId = GetCurrentEmployeeId();
            if (!currentUserId.HasValue)
            {
                return Json(Array.Empty<object>());
            }

            var presets = await _context.UserFilterPresets
                .AsNoTracking()
                .Where(p =>
                    p.UserId == currentUserId.Value &&
                    p.EntityCode == BookingEntityCode &&
                    p.ViewCode == ScheduleViewCode)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var result = presets.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                values = DeserializeFilterValues(p.FiltersJson)
            });

            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveFilterPreset(IFormCollection form)
        {
            var currentUserId = GetCurrentEmployeeId();
            if (!currentUserId.HasValue)
            {
                return Json(new { success = false, message = "Не удалось определить пользователя сессии." });
            }

            var name = form["name"].ToString().Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { success = false, message = "Укажите название фильтра." });
            }

            if (name.Length > 120)
            {
                name = name[..120];
            }

            var values = ReadFilterValuesFromForm(form);
            var serialized = JsonSerializer.Serialize(values);

            var existingPreset = await _context.UserFilterPresets
                .FirstOrDefaultAsync(p =>
                    p.UserId == currentUserId.Value &&
                    p.EntityCode == BookingEntityCode &&
                    p.ViewCode == ScheduleViewCode &&
                    p.Name == name);

            if (existingPreset == null)
            {
                existingPreset = new UserFilterPreset
                {
                    Id = Guid.NewGuid(),
                    UserId = currentUserId.Value,
                    EntityCode = BookingEntityCode,
                    ViewCode = ScheduleViewCode,
                    Name = name,
                    FiltersJson = serialized,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.UserFilterPresets.Add(existingPreset);
            }
            else
            {
                existingPreset.FiltersJson = serialized;
                existingPreset.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                id = existingPreset.Id
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteFilterPreset(Guid id)
        {
            var currentUserId = GetCurrentEmployeeId();
            if (!currentUserId.HasValue)
            {
                return Json(new { success = false, message = "Не удалось определить пользователя сессии." });
            }

            var preset = await _context.UserFilterPresets
                .FirstOrDefaultAsync(p =>
                    p.Id == id &&
                    p.UserId == currentUserId.Value &&
                    p.EntityCode == BookingEntityCode &&
                    p.ViewCode == ScheduleViewCode);

            if (preset == null)
            {
                return Json(new { success = false, message = "Пресет не найден." });
            }

            _context.UserFilterPresets.Remove(preset);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        private async Task EnsureDefaultBookingStatusesAsync()
        {
            var statuses = await _context.BookingStatuses.ToListAsync();

            var hasNew = statuses.Any(s => s.Category == BookingStatusCategory.New);
            var hasIntermediate = statuses.Any(s => s.Category == BookingStatusCategory.Intermediate);
            var hasSuccess = statuses.Any(s => s.Category == BookingStatusCategory.SuccessFinal);
            var hasFailed = statuses.Any(s => s.Category == BookingStatusCategory.FailedFinal);

            var changed = false;

            if (!hasNew)
            {
                _context.BookingStatuses.Add(new BookingStatus
                {
                    Id = Guid.NewGuid(),
                    Name = DefaultNewStatusName,
                    Category = BookingStatusCategory.New,
                    SortOrder = 10,
                    IsActive = true
                });
                changed = true;
            }

            if (!hasIntermediate)
            {
                _context.BookingStatuses.Add(new BookingStatus
                {
                    Id = Guid.NewGuid(),
                    Name = DefaultIntermediateStatusName,
                    Category = BookingStatusCategory.Intermediate,
                    SortOrder = 20,
                    IsActive = true
                });
                changed = true;
            }

            if (!hasSuccess)
            {
                _context.BookingStatuses.Add(new BookingStatus
                {
                    Id = Guid.NewGuid(),
                    Name = DefaultSuccessStatusName,
                    Category = BookingStatusCategory.SuccessFinal,
                    SortOrder = 30,
                    IsActive = true
                });
                changed = true;
            }

            if (!hasFailed)
            {
                _context.BookingStatuses.Add(new BookingStatus
                {
                    Id = Guid.NewGuid(),
                    Name = DefaultFailedStatusName,
                    Category = BookingStatusCategory.FailedFinal,
                    SortOrder = 40,
                    IsActive = true
                });
                changed = true;
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }

        private sealed class BookingLineDraft
        {
            public Guid ServiceItemId { get; init; }
            public int Quantity { get; init; }
            public int DiscountPercent { get; init; }
            public decimal? CustomUnitPrice { get; init; }
        }

        private sealed class BookingItemsBuildResult
        {
            public bool Success { get; init; }
            public string Message { get; init; } = string.Empty;
            public List<CrmResourceBookingItem> Items { get; init; } = new();
            public decimal Amount { get; init; }
        }

        private async Task<BookingItemsBuildResult> BuildBookingItemsFromFormAsync(IFormCollection form)
        {
            var lineDrafts = ReadBookingLineDrafts(form);
            if (!lineDrafts.Any())
            {
                return new BookingItemsBuildResult
                {
                    Success = false,
                    Message = "Выберите хотя бы одну услугу/товар."
                };
            }

            var uniqueServiceIds = lineDrafts
                .Select(x => x.ServiceItemId)
                .Distinct()
                .ToList();

            var services = await _context.ServiceItems
                .AsNoTracking()
                .Where(s => uniqueServiceIds.Contains(s.Id))
                .ToListAsync();

            if (services.Count != uniqueServiceIds.Count)
            {
                return new BookingItemsBuildResult
                {
                    Success = false,
                    Message = "Часть выбранных услуг не найдена в справочнике."
                };
            }

            var serviceMap = services.ToDictionary(s => s.Id, s => s);
            var bookingItems = new List<CrmResourceBookingItem>();
            decimal amount = 0m;

            foreach (var line in lineDrafts)
            {
                var service = serviceMap[line.ServiceItemId];
                var baseUnitPrice = service.Price;

                decimal? customUnitPrice = null;
                if (line.CustomUnitPrice.HasValue)
                {
                    var normalizedCustomPrice = Math.Max(0m, line.CustomUnitPrice.Value);
                    customUnitPrice = Math.Min(baseUnitPrice, normalizedCustomPrice);
                }

                decimal discountPercent;
                decimal effectiveUnitPrice;
                if (customUnitPrice.HasValue)
                {
                    effectiveUnitPrice = customUnitPrice.Value;
                    discountPercent = baseUnitPrice > 0m
                        ? Math.Round(((baseUnitPrice - effectiveUnitPrice) / baseUnitPrice) * 100m, 1, MidpointRounding.AwayFromZero)
                        : 0m;
                }
                else
                {
                    discountPercent = Math.Clamp(line.DiscountPercent, 0, 100);
                    effectiveUnitPrice = baseUnitPrice * (100m - discountPercent) / 100m;
                }

                var lineTotal = Math.Round(effectiveUnitPrice * line.Quantity, 1, MidpointRounding.AwayFromZero);
                amount += lineTotal;

                bookingItems.Add(new CrmResourceBookingItem
                {
                    Id = Guid.NewGuid(),
                    ServiceItemId = line.ServiceItemId,
                    Quantity = line.Quantity,
                    UnitPrice = baseUnitPrice,
                    CustomUnitPrice = customUnitPrice,
                    DiscountAmount = discountPercent,
                    LineTotal = lineTotal
                });
            }

            return new BookingItemsBuildResult
            {
                Success = true,
                Items = bookingItems,
                Amount = Math.Round(amount, 1, MidpointRounding.AwayFromZero)
            };
        }

        private static List<BookingLineDraft> ReadBookingLineDrafts(IFormCollection form)
        {
            var lineServiceIds = form["lineServiceId"];
            if (lineServiceIds.Count > 0)
            {
                var lineQuantities = form["lineQuantity"];
                var lineDiscounts = form["lineDiscountPercent"];
                var lineCustomPrices = form["lineCustomUnitPrice"];

                var result = new List<BookingLineDraft>();
                for (var i = 0; i < lineServiceIds.Count; i++)
                {
                    if (!Guid.TryParse(lineServiceIds[i], out var serviceId) || serviceId == Guid.Empty)
                    {
                        continue;
                    }

                    var quantity = ParsePositiveIntOrDefault(GetValueAt(lineQuantities, i), 1);
                    var discountPercent = ParsePercentageIntOrDefault(GetValueAt(lineDiscounts, i), 0);
                    var customPriceRaw = GetValueAt(lineCustomPrices, i);

                    decimal? customUnitPrice = null;
                    if (!string.IsNullOrWhiteSpace(customPriceRaw))
                    {
                        var parsedCustomPrice = ParseNonNegativeDecimalOrDefault(customPriceRaw, -1m);
                        if (parsedCustomPrice >= 0m)
                        {
                            customUnitPrice = parsedCustomPrice;
                        }
                    }

                    result.Add(new BookingLineDraft
                    {
                        ServiceItemId = serviceId,
                        Quantity = quantity,
                        DiscountPercent = discountPercent,
                        CustomUnitPrice = customUnitPrice
                    });
                }

                if (result.Any())
                {
                    return result;
                }
            }

            var fallbackServiceIds = form["serviceItemIds"]
                .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

            return fallbackServiceIds
                .Select(serviceId =>
                {
                    var qtyRaw = form[$"serviceQty_{serviceId}"].ToString();
                    var discountRaw = form[$"serviceDiscount_{serviceId}"].ToString();
                    var customPriceRaw = form[$"serviceCustomPrice_{serviceId}"].ToString();

                    decimal? customUnitPrice = null;
                    if (!string.IsNullOrWhiteSpace(customPriceRaw))
                    {
                        var parsedCustomPrice = ParseNonNegativeDecimalOrDefault(customPriceRaw, -1m);
                        if (parsedCustomPrice >= 0m)
                        {
                            customUnitPrice = parsedCustomPrice;
                        }
                    }

                    return new BookingLineDraft
                    {
                        ServiceItemId = serviceId,
                        Quantity = ParsePositiveIntOrDefault(qtyRaw, 1),
                        DiscountPercent = ParsePercentageIntOrDefault(discountRaw, 0),
                        CustomUnitPrice = customUnitPrice
                    };
                })
                .ToList();
        }

        private static string GetValueAt(StringValues values, int index)
        {
            if (index < 0 || index >= values.Count)
            {
                return string.Empty;
            }

            return values[index] ?? string.Empty;
        }

        private Dictionary<string, string> ReadFiltersFromQuery()
        {
            return Request.Query
                .Where(x => x.Key.StartsWith("f_", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        }

        private static Guid? ParseOptionalGuid(string? raw)
        {
            return Guid.TryParse(raw, out var parsed) && parsed != Guid.Empty
                ? parsed
                : null;
        }

        private static List<Guid> ParseGuidList(IEnumerable<string> values)
        {
            return values
                .Select(v => Guid.TryParse(v, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
        }

        private static string GetColorByStatusCategory(BookingStatusCategory category)
        {
            return category switch
            {
                BookingStatusCategory.New => "#f59f00",
                BookingStatusCategory.Intermediate => "#0dcaf0",
                BookingStatusCategory.SuccessFinal => "#198754",
                BookingStatusCategory.FailedFinal => "#dc3545",
                _ => "#6c757d"
            };
        }

        private static string GetBorderByStatusCategory(BookingStatusCategory category)
        {
            return category switch
            {
                BookingStatusCategory.New => "#d97706",
                BookingStatusCategory.Intermediate => "#0aa2c0",
                BookingStatusCategory.SuccessFinal => "#157347",
                BookingStatusCategory.FailedFinal => "#b02a37",
                _ => "#565e64"
            };
        }

        private static object MapDecorationToScheduleEvent(BookingCalendarDecoration decoration)
        {
            var backgroundColor = decoration.Kind switch
            {
                BookingCalendarDecorationKind.CompanyLunch => "var(--schedule-company-lunch-color)",
                BookingCalendarDecorationKind.EmployeeAbsence => "var(--schedule-employee-absence-color)",
                _ => "var(--schedule-company-closed-color)"
            };

            var textColor = decoration.Kind switch
            {
                BookingCalendarDecorationKind.CompanyLunch => "var(--schedule-company-lunch-text-color)",
                BookingCalendarDecorationKind.EmployeeAbsence => "var(--schedule-employee-absence-text-color)",
                _ => "var(--schedule-company-closed-text-color)"
            };

            var cssClass = decoration.Kind switch
            {
                BookingCalendarDecorationKind.CompanyLunch => "schedule-calendar-lunch",
                BookingCalendarDecorationKind.EmployeeAbsence => "schedule-calendar-absence",
                _ => "schedule-calendar-closed"
            };

            return new
            {
                id = decoration.Id,
                title = decoration.Title,
                start = decoration.Start.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = decoration.End.ToString("yyyy-MM-ddTHH:mm:ss"),
                display = "background",
                backgroundColor,
                textColor,
                classNames = new[] { cssClass },
                overlap = false,
                editable = false,
                extendedProps = new
                {
                    isCalendarDecoration = true,
                    decorationKind = decoration.Kind.ToString(),
                    isFullDay = decoration.IsFullDay
                }
            };
        }

        private async Task<string> ResolveBookingTitleAsync(
            string? rawTitle,
            Guid performerEmployeeId,
            Guid resourceId,
            Guid? serviceItemId)
        {
            var normalizedTitle = NormalizeBookingTitle(rawTitle);
            if (!string.IsNullOrWhiteSpace(normalizedTitle))
            {
                return normalizedTitle;
            }

            var performerFullName = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == performerEmployeeId)
                .Select(e => (e.LastName + " " + e.FirstName + " " + e.MiddleName).Trim())
                .FirstOrDefaultAsync();

            var resourceName = await _context.CrmResources
                .AsNoTracking()
                .Where(r => r.Id == resourceId)
                .Select(r => r.Name)
                .FirstOrDefaultAsync();

            string? serviceName = null;
            if (serviceItemId.HasValue && serviceItemId.Value != Guid.Empty)
            {
                serviceName = await _context.ServiceItems
                    .AsNoTracking()
                    .Where(s => s.Id == serviceItemId.Value)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync();
            }

            return BuildBookingTitle(
                null,
                performerFullName,
                resourceName,
                serviceName);
        }

        private static string BuildBookingTitle(
            string? customTitle,
            string? performerFullName,
            string? resourceName,
            string? serviceName)
        {
            var normalizedTitle = NormalizeBookingTitle(customTitle);
            if (!string.IsNullOrWhiteSpace(normalizedTitle))
            {
                return normalizedTitle;
            }

            var performerPart = FormatEmployeeShortName(performerFullName);
            var resourcePart = string.IsNullOrWhiteSpace(resourceName) ? "Без ресурса" : resourceName.Trim();
            var servicePart = string.IsNullOrWhiteSpace(serviceName) ? "Без услуги" : serviceName.Trim();

            return $"{performerPart}, {resourcePart}, {servicePart}";
        }

        private static string FormatEmployeeShortName(string? performerFullName)
        {
            var parts = (performerFullName ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0)
            {
                return "Без исполнителя";
            }

            var lastName = parts[0];
            var initials = parts
                .Skip(1)
                .Where(part => part.Length > 0)
                .Take(2)
                .Select(part => $"{char.ToUpperInvariant(part[0])}.")
                .ToList();

            return initials.Count > 0
                ? $"{lastName} {string.Join(" ", initials)}"
                : lastName;
        }

        private static string? NormalizeBookingTitle(string? rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return null;
            }

            return rawTitle.Trim();
        }

        private async Task LogBookingCreatedAsync(CrmResourceBooking booking, Guid? actorId)
        {
            var snapshot = await BuildBookingTimelineSnapshotAsync(booking);
            await _timelineService.LogEventAsync(
                booking.Id,
                BookingEntityCode,
                CrmEventType.System,
                "Запись создана",
                BuildBookingCreatedSummary(snapshot),
                actorId);
        }

        private async Task LogBookingUpdatedAsync(
            CrmResourceBooking before,
            CrmResourceBooking after,
            Guid? actorId,
            string title)
        {
            var fieldLabels = await LoadFieldLabelMapAsync(BookingEntityCode);
            var beforeSnapshot = await BuildBookingTimelineSnapshotAsync(before);
            var afterSnapshot = await BuildBookingTimelineSnapshotAsync(after);

            await _timelineService.LogEventAsync(
                after.Id,
                BookingEntityCode,
                CrmEventType.FieldChange,
                title,
                BuildBookingChangeSummary(beforeSnapshot, afterSnapshot, fieldLabels),
                actorId);
        }

        private async Task LogBookingDeletedAsync(CrmResourceBooking booking, Guid? actorId)
        {
            var snapshot = await BuildBookingTimelineSnapshotAsync(booking);
            var lines = new List<string>
            {
                $"Заголовок: {DisplayOrPlaceholder(snapshot.Title)}",
                $"Период: {FormatBookingPeriod(snapshot.Start, snapshot.End)}"
            };

            await _timelineService.LogEventAsync(
                booking.Id,
                BookingEntityCode,
                CrmEventType.System,
                "Запись перемещена в корзину",
                string.Join(Environment.NewLine, lines),
                actorId);
        }

        private async Task<BookingTimelineSnapshot> BuildBookingTimelineSnapshotAsync(CrmResourceBooking booking)
        {
            var performerName = booking.PerformerEmployee?.FullName;
            if (string.IsNullOrWhiteSpace(performerName) && booking.PerformerEmployeeId.HasValue)
            {
                performerName = await _context.Employees
                    .AsNoTracking()
                    .Where(e => e.Id == booking.PerformerEmployeeId.Value)
                    .Select(e => e.FullName)
                    .FirstOrDefaultAsync();
            }

            var resourceName = booking.Resource?.Name;
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                resourceName = await _context.CrmResources
                    .AsNoTracking()
                    .Where(r => r.Id == booking.ResourceId)
                    .Select(r => r.Name)
                    .FirstOrDefaultAsync();
            }

            var statusName = booking.Status?.Name;
            if (string.IsNullOrWhiteSpace(statusName) && booking.StatusId.HasValue)
            {
                statusName = await _context.BookingStatuses
                    .AsNoTracking()
                    .Where(s => s.Id == booking.StatusId.Value)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync();
            }

            var serviceIds = booking.BookingItems
                .Select(i => i.ServiceItemId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();
            if (!serviceIds.Any() && booking.ServiceItemId.HasValue && booking.ServiceItemId.Value != Guid.Empty)
            {
                serviceIds.Add(booking.ServiceItemId.Value);
            }

            var serviceNames = serviceIds.Any()
                ? await _context.ServiceItems
                    .AsNoTracking()
                    .Where(s => serviceIds.Contains(s.Id))
                    .OrderBy(s => s.Name)
                    .Select(s => s.Name)
                    .ToListAsync()
                : new List<string>();

            var contactIds = booking.BookingContacts
                .Select(c => c.ContactId)
                .Distinct()
                .ToList();

            var contactNames = contactIds.Any()
                ? await _context.Contacts
                    .AsNoTracking()
                    .Where(c => contactIds.Contains(c.Id))
                    .OrderBy(c => c.FullName)
                    .Select(c => c.FullName)
                    .ToListAsync()
                : new List<string>();

            return new BookingTimelineSnapshot(
                booking.Title,
                performerName,
                resourceName,
                statusName,
                booking.StartTime,
                booking.EndTime,
                booking.Amount,
                booking.Comment,
                booking.DiscountReason,
                serviceNames,
                contactNames,
                TimelineChangeFormatter.ParseDynamicProperties(booking.Properties));
        }

        private static string BuildBookingCreatedSummary(BookingTimelineSnapshot snapshot)
        {
            var lines = new List<string>
            {
                $"Заголовок: {DisplayOrPlaceholder(snapshot.Title)}",
                $"Исполнитель: {DisplayOrPlaceholder(snapshot.PerformerName)}",
                $"Ресурс: {DisplayOrPlaceholder(snapshot.ResourceName)}",
                $"Период: {FormatBookingPeriod(snapshot.Start, snapshot.End)}"
            };

            if (snapshot.ServiceNames.Any())
            {
                lines.Add($"Услуги: {string.Join(", ", snapshot.ServiceNames)}");
            }

            if (!string.IsNullOrWhiteSpace(snapshot.StatusName))
            {
                lines.Add($"Статус: {snapshot.StatusName}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string? BuildBookingChangeSummary(
            BookingTimelineSnapshot before,
            BookingTimelineSnapshot after,
            IReadOnlyDictionary<string, string> fieldLabels)
        {
            var changes = new List<string>();

            TimelineChangeFormatter.AddScalarChange(changes, "Заголовок", before.Title, after.Title);
            TimelineChangeFormatter.AddScalarChange(changes, "Исполнитель", before.PerformerName, after.PerformerName);
            TimelineChangeFormatter.AddScalarChange(changes, "Ресурс", before.ResourceName, after.ResourceName);
            TimelineChangeFormatter.AddScalarChange(changes, "Статус", before.StatusName, after.StatusName);
            TimelineChangeFormatter.AddScalarChange(changes, "Начало", FormatDateTime(before.Start), FormatDateTime(after.Start));
            TimelineChangeFormatter.AddScalarChange(changes, "Окончание", FormatDateTime(before.End), FormatDateTime(after.End));
            TimelineChangeFormatter.AddScalarChange(changes, "Сумма", FormatMoney(before.Amount), FormatMoney(after.Amount));
            TimelineChangeFormatter.AddScalarChange(changes, "Комментарий", before.Comment, after.Comment);
            TimelineChangeFormatter.AddScalarChange(changes, "Обоснование скидки", before.DiscountReason, after.DiscountReason);
            TimelineChangeFormatter.AddCollectionChange(changes, "Услуги", before.ServiceNames, after.ServiceNames);
            TimelineChangeFormatter.AddCollectionChange(changes, "Контакты", before.ContactNames, after.ContactNames);
            TimelineChangeFormatter.AddDictionaryChanges(
                changes,
                before.DynamicProps,
                after.DynamicProps,
                key => fieldLabels.TryGetValue(key, out var label) ? label : key);

            return TimelineChangeFormatter.BuildSummary(changes);
        }

        private static IReadOnlyList<BookingDetailsFieldViewModel> BuildBookingDetailsCustomFields(
            CrmResourceBooking booking,
            IReadOnlyCollection<AppFieldDefinition> customFields,
            IReadOnlyDictionary<string, List<SelectListItem>> lookupData)
        {
            if (customFields.Count == 0 || string.IsNullOrWhiteSpace(booking.Properties))
            {
                return Array.Empty<BookingDetailsFieldViewModel>();
            }

            Dictionary<string, JsonElement>? rawValues;
            try
            {
                rawValues = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(booking.Properties);
            }
            catch
            {
                rawValues = null;
            }

            if (rawValues == null || rawValues.Count == 0)
            {
                return Array.Empty<BookingDetailsFieldViewModel>();
            }

            var result = new List<BookingDetailsFieldViewModel>();

            foreach (var field in customFields)
            {
                if (!rawValues.TryGetValue(field.SystemName, out var rawValue))
                {
                    continue;
                }

                var values = BuildBookingFieldValues(field, rawValue, lookupData);
                if (values.Count == 0)
                {
                    continue;
                }

                result.Add(new BookingDetailsFieldViewModel
                {
                    Label = field.Label,
                    Values = values
                });
            }

            return result;
        }

        private static IReadOnlyList<BookingDetailsFieldValueViewModel> BuildBookingFieldValues(
            AppFieldDefinition field,
            JsonElement value,
            IReadOnlyDictionary<string, List<SelectListItem>> lookupData)
        {
            if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
            {
                return Array.Empty<BookingDetailsFieldValueViewModel>();
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray()
                    .Select(item => BuildBookingFieldValue(field, item, lookupData))
                    .Where(item => item != null)
                    .Cast<BookingDetailsFieldValueViewModel>()
                    .ToList();
            }

            var scalar = BuildBookingFieldValue(field, value, lookupData);
            return scalar == null
                ? Array.Empty<BookingDetailsFieldValueViewModel>()
                : new[] { scalar };
        }

        private static BookingDetailsFieldValueViewModel? BuildBookingFieldValue(
            AppFieldDefinition field,
            JsonElement value,
            IReadOnlyDictionary<string, List<SelectListItem>> lookupData)
        {
            string? text = field.DataType switch
            {
                FieldDataType.Boolean => value.ValueKind switch
                {
                    JsonValueKind.True => "Да",
                    JsonValueKind.False => "Нет",
                    _ => null
                },
                FieldDataType.Date => TryFormatJsonDate(value, "dd.MM.yyyy"),
                FieldDataType.DateTime => TryFormatJsonDate(value, "dd.MM.yyyy HH:mm"),
                FieldDataType.Money => TryFormatJsonDecimal(value, " ₽"),
                FieldDataType.Number => TryFormatJsonDecimal(value),
                FieldDataType.EntityLink => ResolveEntityLinkLabel(field, value, lookupData),
                FieldDataType.Select => ResolveEntityLinkLabel(field, value, lookupData),
                FieldDataType.File => value.GetString(),
                _ => value.ToString()
            };

            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (field.DataType == FieldDataType.File)
            {
                return new BookingDetailsFieldValueViewModel
                {
                    Text = Path.GetFileName(text),
                    Url = text
                };
            }

            return new BookingDetailsFieldValueViewModel
            {
                Text = text
            };
        }

        private static string? ResolveEntityLinkLabel(
            AppFieldDefinition field,
            JsonElement value,
            IReadOnlyDictionary<string, List<SelectListItem>> lookupData)
        {
            var rawId = value.ToString();
            if (string.IsNullOrWhiteSpace(rawId))
            {
                return null;
            }

            if (lookupData.TryGetValue(field.SystemName, out var options))
            {
                var option = options.FirstOrDefault(x => string.Equals(x.Value, rawId, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(option?.Text))
                {
                    return option.Text;
                }
            }

            return rawId;
        }

        private static string? TryFormatJsonDate(JsonElement value, string format)
        {
            if (value.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(value.GetString(), out var parsed))
            {
                return parsed.ToString(format);
            }

            return value.ToString();
        }

        private static string? TryFormatJsonDecimal(JsonElement value, string suffix = "")
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            {
                return $"{number:0.##}{suffix}";
            }

            if (value.ValueKind == JsonValueKind.String &&
                decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number))
            {
                return $"{number:0.##}{suffix}";
            }

            return value.ToString();
        }

        private static string FormatBookingPeriod(DateTime start, DateTime end)
        {
            if (start.Date == end.Date)
            {
                return $"{start:dd.MM.yyyy HH:mm} - {end:HH:mm}";
            }

            return $"{start:dd.MM.yyyy HH:mm} - {end:dd.MM.yyyy HH:mm}";
        }

        private static string FormatDateTime(DateTime value) => value.ToString("dd.MM.yyyy HH:mm");

        private static string FormatMoney(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        private static string DisplayOrPlaceholder(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "не заполнено" : value.Trim();

        private static Dictionary<string, string> ReadFilterValuesFromForm(IFormCollection form)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in form.Keys.Where(k => k.StartsWith("values[", StringComparison.OrdinalIgnoreCase) && k.EndsWith("]")))
            {
                var logicalKey = key["values[".Length..^1];
                values[logicalKey] = form[key].ToString();
            }

            return values;
        }

        private static Dictionary<string, string> DeserializeFilterValues(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ??
                       new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private async Task<bool> IsOutsideCompanyWorkHoursAsync(DateTime start, DateTime end)
        {
            if (start >= end || start.Date != end.Date)
            {
                return true;
            }

            var isHoliday = await _context.CompanyHolidays
                .AsNoTracking()
                .AnyAsync(h => h.Date.Date == start.Date);
            if (isHoliday)
            {
                return true;
            }

            var workMode = await _context.CompanyWorkModes
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.DayOfWeek == start.DayOfWeek);

            if (workMode == null || workMode.IsWeekend)
            {
                return true;
            }

            if (start.TimeOfDay < workMode.StartTime || end.TimeOfDay > workMode.EndTime)
            {
                return true;
            }

            if (workMode.LunchStartTime.HasValue && workMode.LunchEndTime.HasValue)
            {
                var lunchStart = start.Date + workMode.LunchStartTime.Value;
                var lunchEnd = start.Date + workMode.LunchEndTime.Value;
                if (start < lunchEnd && end > lunchStart)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<SlotAvailabilityPreview> EvaluateSlotAvailabilityAsync(
            Guid resourceId,
            Guid performerEmployeeId,
            DateTime start,
            DateTime end,
            Guid? bookingId,
            string? createSource)
        {
            if (resourceId == Guid.Empty)
            {
                return new SlotAvailabilityPreview(false, false, false, "Выберите ресурс.");
            }

            if (performerEmployeeId == Guid.Empty)
            {
                return new SlotAvailabilityPreview(false, false, false, "Выберите исполнителя.");
            }

            if (start >= end)
            {
                return new SlotAvailabilityPreview(false, false, false, "Время окончания должно быть позже времени начала.");
            }

            var isOutsideCompanyWorkHours = await IsOutsideCompanyWorkHoursAsync(start, end);
            if (isOutsideCompanyWorkHours &&
                !bookingId.HasValue &&
                !string.Equals(createSource, "button", StringComparison.OrdinalIgnoreCase))
            {
                return new SlotAvailabilityPreview(
                    false,
                    false,
                    false,
                    "Запись вне рабочего времени можно создать только через кнопку \"Добавить запись\".");
            }

            var availability = await _resourceManager.CheckAvailabilityAsync(
                resourceId,
                start,
                end,
                performerEmployeeId,
                isOutsideCompanyWorkHours,
                bookingId);

            if (!availability.Success)
            {
                return new SlotAvailabilityPreview(
                    false,
                    false,
                    availability.IsOverbooking,
                    availability.Message);
            }

            var requiresConfirmation = isOutsideCompanyWorkHours;
            var message = BuildSlotAvailabilityMessage(
                requiresConfirmation,
                availability.IsOverbooking,
                availability.Message);

            return new SlotAvailabilityPreview(
                true,
                requiresConfirmation,
                availability.IsOverbooking,
                message);
        }

        private static string BuildSlotAvailabilityMessage(
            bool requiresConfirmation,
            bool isOverbooking,
            string? defaultMessage)
        {
            if (requiresConfirmation && isOverbooking)
            {
                return "Время доступно как овербукинг, но при сохранении потребуется подтверждение, потому что запись выходит за рамки графика компании.";
            }

            if (requiresConfirmation)
            {
                return "Время доступно, но при сохранении потребуется подтверждение, потому что запись выходит за рамки графика компании.";
            }

            if (isOverbooking)
            {
                return string.IsNullOrWhiteSpace(defaultMessage)
                    ? "Время доступно, запись будет создана как овербукинг."
                    : defaultMessage;
            }

            return string.Empty;
        }

        private Guid? GetCurrentEmployeeId()
        {
            var employeeIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(employeeIdRaw, out var employeeId) ? employeeId : null;
        }

        private async Task<string> GetEmployeeDisplayNameAsync(Guid? employeeId)
        {
            if (!employeeId.HasValue)
            {
                return "Неизвестный пользователь";
            }

            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == employeeId.Value);

            return employee?.FullName ?? "Неизвестный пользователь";
        }

        private static int ParsePositiveIntOrDefault(string? raw, int fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            if (int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
            {
                return parsed;
            }

            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed) && parsed > 0)
            {
                return parsed;
            }

            return fallback;
        }

        private static int ParsePercentageIntOrDefault(string? raw, int fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            var match = System.Text.RegularExpressions.Regex.Match(raw, "\\d+");
            if (!match.Success)
            {
                return fallback;
            }

            if (!int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return fallback;
            }

            return Math.Clamp(parsed, 0, 100);
        }

        private static bool ParseBoolean(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var normalized = raw.Trim();
            if (bool.TryParse(normalized, out var parsed))
            {
                return parsed;
            }

            return normalized == "1" ||
                   normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   normalized.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private static decimal ParseNonNegativeDecimalOrDefault(string? raw, decimal fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            var normalized = raw.Trim().Replace(',', '.');

            if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
            {
                return parsed;
            }

            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed) && parsed >= 0)
            {
                return parsed;
            }

            return fallback;
        }

        private sealed record SlotAvailabilityPreview(
            bool CanSave,
            bool RequiresConfirmation,
            bool IsOverbooking,
            string Message);

        private sealed record BookingTimelineSnapshot(
            string? Title,
            string? PerformerName,
            string? ResourceName,
            string? StatusName,
            DateTime Start,
            DateTime End,
            decimal Amount,
            string? Comment,
            string? DiscountReason,
            IReadOnlyList<string> ServiceNames,
            IReadOnlyList<string> ContactNames,
            IReadOnlyDictionary<string, string> DynamicProps);
    }
}
