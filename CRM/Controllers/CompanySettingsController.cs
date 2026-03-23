using Core.Constants;
using Core.Data;
using Core.Entities.Company;
using Core.Entities.CRM;
using Core.Entities.System;
using Core.Interfaces.Platform;
using Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CRM.Controllers
{
    public class CompanySettingsController : BasePlatformController
    {
        private const string DefaultNewStatusName = "Новая запись";
        private const string DefaultIntermediateStatusName = "В работе";
        private const string DefaultSuccessStatusName = "Успешно";
        private const string DefaultFailedStatusName = "Неуспешно";

        private readonly ICrmStyleService _styleService;
        private readonly IBookingPolicyService _bookingPolicyService;
        private readonly IFeatureToggleService _featureToggleService;

        public CompanySettingsController(
            AppDbContext context,
            IWebHostEnvironment hostingEnvironment,
            ICrmStyleService styleService,
            IBookingPolicyService bookingPolicyService,
            IFeatureToggleService featureToggleService)
            : base(context, hostingEnvironment)
        {
            _styleService = styleService;
            _bookingPolicyService = bookingPolicyService;
            _featureToggleService = featureToggleService;
        }

        /// <summary>
        /// Страница настройки оформления интерфейса CRM.
        /// </summary>
        public IActionResult InterfaceSettings()
        {
            var settings = _styleService.GetSettings();
            return View(settings);
        }

        /// <summary>
        /// Сохранение настроек оформления.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveInterfaceSettings(UiSettings settings, IFormFile? logoFile)
        {
            if (settings == null) return RedirectToAction(nameof(InterfaceSettings));

            if (logoFile != null && logoFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", "logo");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var fileName = Guid.NewGuid() + Path.GetExtension(logoFile.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                await using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await logoFile.CopyToAsync(fileStream);
                }

                if (!string.IsNullOrEmpty(settings.LogoPath))
                {
                    var oldPath = Path.Combine(_hostingEnvironment.WebRootPath, settings.LogoPath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
                }

                settings.LogoPath = "/uploads/logo/" + fileName;
            }
            else
            {
                var currentSettings = _styleService.GetSettings();
                settings.LogoPath = currentSettings.LogoPath;
            }

            await _styleService.SaveSettingsAsync(settings);

            return RedirectToAction(nameof(InterfaceSettings));
        }

        // Страница настройки режима работы, праздников и политик платформы
        public async Task<IActionResult> WorkMode()
        {
            await EnsureDefaultBookingStatusesAsync();

            var modes = await _context.CompanyWorkModes
                .OrderBy(x => x.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)x.DayOfWeek)
                .ToListAsync();

            if (!modes.Any())
            {
                var days = new[]
                {
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
                        IsWeekend = day == DayOfWeek.Saturday || day == DayOfWeek.Sunday
                    });
                }

                await _context.SaveChangesAsync();
                modes = await _context.CompanyWorkModes
                    .OrderBy(x => x.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)x.DayOfWeek)
                    .ToListAsync();
            }

            ViewBag.Holidays = await _context.CompanyHolidays
                .OrderBy(h => h.Date)
                .ToListAsync();

            ViewBag.BookingPolicy = await _bookingPolicyService.GetGlobalPolicyAsync();

            var toggles = await _featureToggleService.GetAllAsync();
            ViewBag.CrmFeatureEnabled = toggles.TryGetValue(PlatformFeatures.Crm, out var crmEnabled) ? crmEnabled : true;
            ViewBag.BookingFeatureEnabled = toggles.TryGetValue(PlatformFeatures.Booking, out var bookingEnabled)
                ? bookingEnabled
                : true;
            ViewBag.Resources = await _context.CrmResources.OrderBy(r => r.Name).ToListAsync();
            ViewBag.BookingStatuses = await _context.BookingStatuses
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Name)
                .ToListAsync();

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
                _context.CompanyHolidays.Add(new CompanyHoliday
                {
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

        [HttpPost]
        public async Task<IActionResult> SaveBookingPolicy(
            bool allowOverbooking,
            int maxParallelBookings = 2,
            bool allowManualItemPriceChange = false)
        {
            await _bookingPolicyService.SaveGlobalPolicyAsync(
                allowOverbooking,
                maxParallelBookings,
                allowManualItemPriceChange);
            return RedirectToAction(nameof(WorkMode));
        }

        [HttpPost]
        public async Task<IActionResult> SaveModuleSettings(bool crmEnabled, bool bookingEnabled)
        {
            await _featureToggleService.SetAsync(PlatformFeatures.Crm, crmEnabled);
            await _featureToggleService.SetAsync(PlatformFeatures.Booking, bookingEnabled);
            return RedirectToAction(nameof(WorkMode));
        }

        [HttpPost]
        public async Task<IActionResult> CreateBookingResource(
            string name,
            string? description,
            bool? allowOverbooking,
            int? maxParallelBookings)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Укажите название ресурса.";
                return RedirectToAction(nameof(WorkMode));
            }

            var resource = new CrmResource
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                AllowOverbooking = allowOverbooking,
                MaxParallelBookings = maxParallelBookings,
                IsActive = true
            };

            _context.CrmResources.Add(resource);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Ресурс создан.";
            return RedirectToAction(nameof(WorkMode));
        }

        [HttpPost]
        public async Task<IActionResult> SetBookingResourceActive(Guid id, bool isActive)
        {
            var resource = await _context.CrmResources.FindAsync(id);
            if (resource == null)
            {
                TempData["Error"] = "Ресурс не найден.";
                return RedirectToAction(nameof(WorkMode));
            }

            resource.IsActive = isActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = isActive ? "Ресурс активирован." : "Ресурс деактивирован.";
            return RedirectToAction(nameof(WorkMode));
        }

        [HttpPost]
        public async Task<IActionResult> CreateBookingStatus(string name, BookingStatusCategory category)
        {
            name = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Укажите название статуса.";
                return RedirectToAction(nameof(WorkMode));
            }

            var uniquenessError = await ValidateStatusCategoryUniquenessAsync(null, category);
            if (!string.IsNullOrWhiteSpace(uniquenessError))
            {
                TempData["Error"] = uniquenessError;
                return RedirectToAction(nameof(WorkMode));
            }

            var maxSort = await _context.BookingStatuses
                .Select(s => (int?)s.SortOrder)
                .MaxAsync() ?? 0;

            _context.BookingStatuses.Add(new BookingStatus
            {
                Id = Guid.NewGuid(),
                Name = name,
                Category = category,
                IsActive = true,
                SortOrder = maxSort + 10,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Статус добавлен.";
            return RedirectToAction(nameof(WorkMode));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBookingStatus(
            Guid id,
            string name,
            BookingStatusCategory category,
            bool isActive,
            int sortOrder = 100)
        {
            var status = await _context.BookingStatuses.FindAsync(id);
            if (status == null)
            {
                TempData["Error"] = "Статус не найден.";
                return RedirectToAction(nameof(WorkMode));
            }

            name = (name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Укажите название статуса.";
                return RedirectToAction(nameof(WorkMode));
            }

            var uniquenessError = await ValidateStatusCategoryUniquenessAsync(id, category);
            if (!string.IsNullOrWhiteSpace(uniquenessError))
            {
                TempData["Error"] = uniquenessError;
                return RedirectToAction(nameof(WorkMode));
            }

            status.Name = name;
            status.Category = category;
            status.IsActive = isActive;
            status.SortOrder = sortOrder;
            status.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Статус обновлён.";
            return RedirectToAction(nameof(WorkMode));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBookingStatus(Guid id)
        {
            var status = await _context.BookingStatuses.FindAsync(id);
            if (status == null)
            {
                TempData["Error"] = "Статус не найден.";
                return RedirectToAction(nameof(WorkMode));
            }

            _context.BookingStatuses.Remove(status);
            await _context.SaveChangesAsync();

            await EnsureDefaultBookingStatusesAsync();
            TempData["Success"] = "Статус удалён.";
            return RedirectToAction(nameof(WorkMode));
        }

        private async Task<string?> ValidateStatusCategoryUniquenessAsync(Guid? currentId, BookingStatusCategory category)
        {
            if (category != BookingStatusCategory.New && category != BookingStatusCategory.SuccessFinal)
            {
                return null;
            }

            var exists = await _context.BookingStatuses
                .AnyAsync(s =>
                    s.Category == category &&
                    (!currentId.HasValue || s.Id != currentId.Value));

            if (!exists)
            {
                return null;
            }

            return category == BookingStatusCategory.New
                ? "Статус категории 'Новая запись' может быть только один."
                : "Статус категории 'Завершённый успешный' может быть только один.";
        }

        private async Task EnsureDefaultBookingStatusesAsync()
        {
            var statuses = await _context.BookingStatuses.ToListAsync();
            var changed = false;

            if (!statuses.Any(s => s.Category == BookingStatusCategory.New))
            {
                _context.BookingStatuses.Add(new BookingStatus
                {
                    Id = Guid.NewGuid(),
                    Name = DefaultNewStatusName,
                    Category = BookingStatusCategory.New,
                    IsActive = true,
                    SortOrder = 10
                });
                changed = true;
            }

            if (!statuses.Any(s => s.Category == BookingStatusCategory.Intermediate))
            {
                _context.BookingStatuses.Add(new BookingStatus
                {
                    Id = Guid.NewGuid(),
                    Name = DefaultIntermediateStatusName,
                    Category = BookingStatusCategory.Intermediate,
                    IsActive = true,
                    SortOrder = 20
                });
                changed = true;
            }

            if (!statuses.Any(s => s.Category == BookingStatusCategory.SuccessFinal))
            {
                _context.BookingStatuses.Add(new BookingStatus
                {
                    Id = Guid.NewGuid(),
                    Name = DefaultSuccessStatusName,
                    Category = BookingStatusCategory.SuccessFinal,
                    IsActive = true,
                    SortOrder = 30
                });
                changed = true;
            }

            if (!statuses.Any(s => s.Category == BookingStatusCategory.FailedFinal))
            {
                _context.BookingStatuses.Add(new BookingStatus
                {
                    Id = Guid.NewGuid(),
                    Name = DefaultFailedStatusName,
                    Category = BookingStatusCategory.FailedFinal,
                    IsActive = true,
                    SortOrder = 40
                });
                changed = true;
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}
