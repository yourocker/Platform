using Core.Data;
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

        public NavigationViewComponent(AppDbContext context)
        {
            _context = context;
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

            var model = new NavigationViewModel
            {
                CustomCategories = categories,
                CrmCategory = crmCategory,
                Controller = controller,
                Action = action,
                EntityCode = entityCode,

                IsCRMActive = controller == "Contacts" || 
                              categories.Any(c => c.IsSystem && c.Name == "CRM" && c.Apps.Any(a => a.EntityCode == entityCode)),

                IsCompanyActive = (new[] { "Positions", "Departments", "Employees", "EmployeeSchedule" }).Contains(controller) ||
                                  categories.Any(c => c.IsSystem && c.Name == "Компания" && c.Apps.Any(a => a.EntityCode == entityCode)),

                IsTasksActive = controller == "Tasks",

                IsSettingsActive = (new[] { "AppDefinitions", "AppCategories", "CompanySettings", "Services" }).Contains(controller)
            };

            return View(model);
        }
    }
}