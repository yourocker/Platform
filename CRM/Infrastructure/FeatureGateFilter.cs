using Core.Constants;
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
            "Leads"
        };

        private static readonly HashSet<string> CrmEntityCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Contact",
            "Lead",
            "Deal"
        };

        private static readonly HashSet<string> BookingControllers = new(StringComparer.OrdinalIgnoreCase)
        {
            "Schedule"
        };

        private readonly IFeatureToggleService _featureToggleService;

        public FeatureGateFilter(IFeatureToggleService featureToggleService)
        {
            _featureToggleService = featureToggleService;
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

            if (controller.Equals("Data", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(entityCode) &&
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
    }
}
