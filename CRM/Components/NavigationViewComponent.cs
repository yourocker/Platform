using Core.Data;
using Core.Constants;
using Core.Interfaces.Platform;
using CRM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Components
{
    public class NavigationViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;
        private readonly IFeatureToggleService _featureToggleService;

        public NavigationViewComponent(
            AppDbContext context,
            IFeatureToggleService featureToggleService)
        {
            _context = context;
            _featureToggleService = featureToggleService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var routeData = ViewContext.RouteData.Values;
            var controller = routeData["Controller"]?.ToString();
            var action = routeData["Action"]?.ToString();
            var entityCode = routeData["entityCode"]?.ToString() ?? ViewBag.EntityCode?.ToString();

            var categories = await _context.AppCategories
                .Include(c => c.Apps)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

            var crmCategory = categories.FirstOrDefault(c => c.Name == "CRM");
            var crmModuleEnabled = await _featureToggleService.IsEnabledAsync(PlatformFeatures.Crm);
            var bookingModuleEnabled = await _featureToggleService.IsEnabledAsync(PlatformFeatures.Booking);

            if (!crmModuleEnabled)
            {
                crmCategory = null;
            }

            var model = new NavigationViewModel
            {
                CustomCategories = categories,
                CrmCategory = crmCategory,
                Controller = controller,
                Action = action,
                EntityCode = entityCode,
                IsCrmModuleEnabled = crmModuleEnabled,
                IsBookingModuleEnabled = bookingModuleEnabled,

                IsCRMActive = crmModuleEnabled &&
                              (controller == "Contacts" ||
                               categories.Any(c => c.IsSystem && c.Name == "CRM" && c.Apps.Any(a => a.EntityCode == entityCode))),

                IsScheduleActive = bookingModuleEnabled &&
                                   controller == "Schedule",

                IsCompanyActive = (new[] { "Positions", "Departments", "Employees", "EmployeeSchedule", "CompanySettings", "Services" }).Contains(controller) ||
                                  categories.Any(c => c.IsSystem && c.Name == "Компания" && c.Apps.Any(a => a.EntityCode == entityCode)),

                IsTasksActive = controller == "Tasks",

                IsSettingsActive = (new[] { "AppDefinitions", "AppCategories" }).Contains(controller)
            };

            return View(model);
        }
    }
}
