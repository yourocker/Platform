using Core.Data;
using Core.Constants;
using Core.Interfaces.CRM;
using Core.Interfaces.Platform;
using CRM.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Components
{
    public class NavigationViewComponent : ViewComponent
    {
        private readonly AppDbContext _context;
        private readonly IFeatureToggleService _featureToggleService;
        private readonly ICrmSettingsService _crmSettingsService;

        public NavigationViewComponent(
            AppDbContext context,
            IFeatureToggleService featureToggleService,
            ICrmSettingsService crmSettingsService)
        {
            _context = context;
            _featureToggleService = featureToggleService;
            _crmSettingsService = crmSettingsService;
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

            var homeCategory = categories.FirstOrDefault(c => c.Name == "Рабочий стол");
            var crmCategory = categories.FirstOrDefault(c => c.Name == "CRM");
            var companyCategory = categories.FirstOrDefault(c => c.Name == "Компания");
            var scheduleCategory = categories.FirstOrDefault(c => c.Name == "Расписание");
            var tasksCategory = categories.FirstOrDefault(c => c.Name == "Задачи");
            var crmModuleEnabled = await _featureToggleService.IsEnabledAsync(PlatformFeatures.Crm);
            var bookingModuleEnabled = await _featureToggleService.IsEnabledAsync(PlatformFeatures.Booking);
            var useLeads = crmModuleEnabled && await _crmSettingsService.UseLeadsAsync();
            var crmApps = crmCategory?.Apps
                .Where(app => useLeads || !string.Equals(app.EntityCode, "Lead", System.StringComparison.OrdinalIgnoreCase))
                .ToList() ?? new List<Core.Entities.Platform.AppDefinition>();

            if (!crmModuleEnabled)
            {
                crmCategory = null;
                crmApps = new List<Core.Entities.Platform.AppDefinition>();
            }

            if (!bookingModuleEnabled)
            {
                scheduleCategory = null;
            }

            var crmDefaultUrl = "/Contacts";
            if (crmModuleEnabled)
            {
                if (useLeads && crmApps.Any(a => string.Equals(a.EntityCode, "Lead", System.StringComparison.OrdinalIgnoreCase)))
                {
                    crmDefaultUrl = "/Leads";
                }
                else if (crmApps.Any(a => string.Equals(a.EntityCode, "Deal", System.StringComparison.OrdinalIgnoreCase)))
                {
                    crmDefaultUrl = "/Deals";
                }
                else if (crmApps.Any(a => string.Equals(a.EntityCode, "Contact", System.StringComparison.OrdinalIgnoreCase)))
                {
                    crmDefaultUrl = "/Contacts";
                }
                else if (crmApps.Any(a => string.Equals(a.EntityCode, "Company", System.StringComparison.OrdinalIgnoreCase)))
                {
                    crmDefaultUrl = "/Data/Company";
                }
            }

            var model = new NavigationViewModel
            {
                CustomCategories = categories,
                HomeCategory = homeCategory,
                CrmCategory = crmCategory,
                CrmApps = crmApps,
                CompanyCategory = companyCategory,
                ScheduleCategory = scheduleCategory,
                TasksCategory = tasksCategory,
                Controller = controller,
                Action = action,
                EntityCode = entityCode,
                IsCrmModuleEnabled = crmModuleEnabled,
                IsBookingModuleEnabled = bookingModuleEnabled,
                UseLeads = useLeads,
                CrmDefaultUrl = crmDefaultUrl,

                IsHomeActive = controller == "Home" ||
                               categories.Any(c => c.IsSystem && c.Name == "Рабочий стол" && c.Apps.Any(a => a.EntityCode == entityCode)),

                IsCRMActive = crmModuleEnabled &&
                              ((new[] { "Contacts", "Leads", "Deals" }).Contains(controller) ||
                               crmApps.Any(a => a.EntityCode == entityCode)),

                IsScheduleActive = bookingModuleEnabled &&
                                   (controller == "Schedule" ||
                                    categories.Any(c => c.IsSystem && c.Name == "Расписание" && c.Apps.Any(a => a.EntityCode == entityCode))),

                IsCompanyActive = (new[] { "Positions", "Departments", "Employees", "EmployeeSchedule", "CompanySettings", "Services" }).Contains(controller) ||
                                  categories.Any(c => c.IsSystem && c.Name == "Компания" && c.Apps.Any(a => a.EntityCode == entityCode)),

                IsTasksActive = controller == "Tasks" ||
                                categories.Any(c => c.IsSystem && c.Name == "Задачи" && c.Apps.Any(a => a.EntityCode == entityCode)),

                IsSettingsActive = (new[] { "AppDefinitions", "AppCategories" }).Contains(controller)
            };

            return View(model);
        }
    }
}
