using Core.Constants;
using Core.Data;
using Core.Entities.Company;
using Core.Entities.CRM;
using Core.Entities.System;
using Core.Interfaces.CRM;
using Core.Interfaces.Platform;
using Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRM.Infrastructure.Security;
using CRM.Infrastructure.Trash;
using CRM.ViewModels.CompanySettings;

namespace CRM.Controllers
{
    [TenantAuthorize(TenantPermissions.ManageTenantSettings)]
    public class CompanySettingsController : BasePlatformController
    {
        private const string DefaultNewStatusName = "Новая запись";
        private const string DefaultIntermediateStatusName = "В работе";
        private const string DefaultSuccessStatusName = "Успешно";
        private const string DefaultFailedStatusName = "Неуспешно";

        private readonly ICrmStyleService _styleService;
        private readonly IBookingPolicyService _bookingPolicyService;
        private readonly IFeatureToggleService _featureToggleService;
        private readonly ICrmSettingsService _crmSettingsService;
        private readonly ITrashService _trashService;
        private readonly ITenantMembershipAdministrationService _tenantMembershipAdministrationService;

        public CompanySettingsController(
            AppDbContext context,
            IWebHostEnvironment hostingEnvironment,
            ICrmStyleService styleService,
            IBookingPolicyService bookingPolicyService,
            IFeatureToggleService featureToggleService,
            ICrmSettingsService crmSettingsService,
            ITrashService trashService,
            ITenantMembershipAdministrationService tenantMembershipAdministrationService)
            : base(context, hostingEnvironment)
        {
            _styleService = styleService;
            _bookingPolicyService = bookingPolicyService;
            _featureToggleService = featureToggleService;
            _crmSettingsService = crmSettingsService;
            _trashService = trashService;
            _tenantMembershipAdministrationService = tenantMembershipAdministrationService;
        }

        /// <summary>
        /// Страница настройки оформления интерфейса CRM.
        /// </summary>
        public async Task<IActionResult> InterfaceSettings()
        {
            var settings = _styleService.GetSettings();
            ViewBag.UseLeadsEnabled = await _crmSettingsService.UseLeadsAsync();
            return View(settings);
        }

        /// <summary>
        /// Сохранение настроек оформления.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveInterfaceSettings(UiSettings settings, bool useLeads, IFormFile? logoFile)
        {
            if (settings == null) return RedirectToAction(nameof(InterfaceSettings));

            var currentSettings = _styleService.GetSettings();
            if (settings.Id == Guid.Empty)
            {
                settings.Id = currentSettings.Id;
            }

            if (logoFile != null && logoFile.Length > 0)
            {
                var storedFile = await FileStorageService.SaveAsync(
                    logoFile,
                    "company-logo",
                    nameof(UiSettings),
                    settings.Id == Guid.Empty ? null : settings.Id);

                DeletePhysicalFile(currentSettings.LogoPath);
                settings.LogoPath = FileStorageService.BuildAccessPath(storedFile.Id);
            }
            else
            {
                settings.LogoPath = currentSettings.LogoPath;
            }

            await _styleService.SaveSettingsAsync(settings);
            await _crmSettingsService.SetUseLeadsAsync(useLeads);

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

        [HttpGet]
        [TenantAuthorize(TenantPermissions.ManageTenantMembers)]
        public async Task<IActionResult> TenantMembers([FromQuery] TenantMembersFilterInput filter, CancellationToken cancellationToken)
        {
            var model = await _tenantMembershipAdministrationService.GetPageModelAsync(filter, User, cancellationToken);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantAuthorize(TenantPermissions.ManageTenantMembers)]
        public async Task<IActionResult> InviteTenantMember(
            InviteTenantMemberInput input,
            string? returnUrl = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _tenantMembershipAdministrationService.InviteAsync(User, input, cancellationToken);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectBackToTenantMembers(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantAuthorize(TenantPermissions.ManageTenantMembers)]
        public async Task<IActionResult> UpdateTenantMemberRole(
            Guid membershipId,
            string roleCode,
            string? returnUrl = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _tenantMembershipAdministrationService.UpdateRoleAsync(User, membershipId, roleCode, cancellationToken);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectBackToTenantMembers(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantAuthorize(TenantPermissions.ManageTenantMembers)]
        public async Task<IActionResult> SetTenantMemberActive(
            Guid membershipId,
            bool isActive,
            string? returnUrl = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _tenantMembershipAdministrationService.SetActiveAsync(User, membershipId, isActive, cancellationToken);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectBackToTenantMembers(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [TenantAuthorize(TenantPermissions.ManageTenantMembers)]
        public async Task<IActionResult> SetTenantMemberDefault(
            Guid membershipId,
            string? returnUrl = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _tenantMembershipAdministrationService.SetDefaultAsync(User, membershipId, cancellationToken);
            TempData[result.Success ? "Success" : "Error"] = result.Message;
            return RedirectBackToTenantMembers(returnUrl);
        }

        [HttpGet]
        public async Task<IActionResult> Trash([FromQuery] TrashFilterInput filter, CancellationToken cancellationToken)
        {
            var model = await _trashService.GetPageModelAsync(filter, cancellationToken);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreTrashItem(string selectionKey, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var restoredCount = await _trashService.RestoreAsync(new[] { selectionKey }, cancellationToken);
            TempData[restoredCount > 0 ? "Success" : "Error"] = restoredCount > 0
                ? "Запись восстановлена."
                : "Не удалось восстановить запись.";
            return RedirectBackToTrash(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermanentlyDeleteTrashItem(string selectionKey, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var deletedCount = await _trashService.PermanentlyDeleteAsync(new[] { selectionKey }, cancellationToken);
            TempData[deletedCount > 0 ? "Success" : "Error"] = deletedCount > 0
                ? "Запись удалена безвозвратно."
                : "Не удалось удалить запись безвозвратно.";
            return RedirectBackToTrash(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreTrashBulk(string[]? selectionKeys, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var restoredCount = await _trashService.RestoreAsync(selectionKeys, cancellationToken);
            TempData[restoredCount > 0 ? "Success" : "Error"] = restoredCount > 0
                ? $"Восстановлено записей: {restoredCount}."
                : "Для восстановления не выбрано ни одной записи.";
            return RedirectBackToTrash(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermanentlyDeleteTrashBulk(string[]? selectionKeys, string? returnUrl = null, CancellationToken cancellationToken = default)
        {
            var deletedCount = await _trashService.PermanentlyDeleteAsync(selectionKeys, cancellationToken);
            TempData[deletedCount > 0 ? "Success" : "Error"] = deletedCount > 0
                ? $"Удалено безвозвратно записей: {deletedCount}."
                : "Для безвозвратного удаления не выбрано ни одной записи.";
            return RedirectBackToTrash(returnUrl);
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

        private IActionResult RedirectBackToTrash(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Trash));
        }

        private IActionResult RedirectBackToTenantMembers(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(TenantMembers));
        }
    }
}
