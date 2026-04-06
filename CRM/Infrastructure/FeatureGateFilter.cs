using Core.Constants;
using Core.Interfaces.CRM;
using Core.Interfaces.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CRM.Infrastructure
{
    /// <summary>
    /// Центральная точка ограничения модулей по тарифу.
    /// </summary>
    public class FeatureGateFilter : IAsyncActionFilter
    {
        private static readonly HashSet<string> CrmControllers = new(StringComparer.OrdinalIgnoreCase)
        {
            "Contacts",
            "Leads",
            "Deals"
        };

        private static readonly HashSet<string> CrmEntityCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Contact",
            "Company",
            "Lead",
            "Deal"
        };

        private static readonly HashSet<string> BookingControllers = new(StringComparer.OrdinalIgnoreCase)
        {
            "Schedule"
        };

        private readonly IFeatureToggleService _featureToggleService;
        private readonly ICrmSettingsService _crmSettingsService;

        public FeatureGateFilter(
            IFeatureToggleService featureToggleService,
            ICrmSettingsService crmSettingsService)
        {
            _featureToggleService = featureToggleService;
            _crmSettingsService = crmSettingsService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controller = context.RouteData.Values["controller"]?.ToString();
            var entityCode = context.RouteData.Values["entityCode"]?.ToString();

            if (RequiresCrmFeature(controller, entityCode))
            {
                var isEnabled = await _featureToggleService.IsEnabledAsync(PlatformFeatures.Crm);
                if (!isEnabled)
                {
                    context.Result = new RedirectToActionResult("Index", "Home", null);
                    return;
                }
            }

            if (RequiresLeadMode(controller, entityCode))
            {
                var useLeads = await _crmSettingsService.UseLeadsAsync();
                if (!useLeads)
                {
                    context.Result = new RedirectToActionResult("Index", "Contacts", null);
                    return;
                }
            }

            if (RequiresBookingFeature(controller))
            {
                var isEnabled = await _featureToggleService.IsEnabledAsync(PlatformFeatures.Booking);
                if (!isEnabled)
                {
                    context.Result = new RedirectToActionResult("Index", "Home", null);
                    return;
                }
            }

            await next();
        }

        private static bool RequiresCrmFeature(string? controller, string? entityCode)
        {
            if (string.IsNullOrWhiteSpace(controller))
            {
                return false;
            }

            if (CrmControllers.Contains(controller))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(entityCode) &&
                CrmEntityCodes.Contains(entityCode))
            {
                return true;
            }

            return false;
        }

        private static bool RequiresBookingFeature(string? controller)
        {
            return !string.IsNullOrWhiteSpace(controller) && BookingControllers.Contains(controller);
        }

        private static bool RequiresLeadMode(string? controller, string? entityCode)
        {
            if (!string.IsNullOrWhiteSpace(controller) &&
                controller.Equals("Leads", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(entityCode) &&
                   entityCode.Equals("Lead", StringComparison.OrdinalIgnoreCase);
        }
    }
}
