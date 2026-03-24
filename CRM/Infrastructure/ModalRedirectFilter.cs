using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CRM.Infrastructure;

public sealed class ModalRedirectFilter(IUrlHelperFactory urlHelperFactory) : IAsyncAlwaysRunResultFilter
{
    private readonly IUrlHelperFactory _urlHelperFactory = urlHelperFactory;

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (!ModalRequestHelper.IsModalRequest(context.HttpContext.Request))
        {
            await next();
            return;
        }

        var redirectUrl = ResolveRedirectUrl(context);
        if (string.IsNullOrWhiteSpace(redirectUrl))
        {
            await next();
            return;
        }

        context.Result = ModalRequestHelper.BuildRedirectContent(redirectUrl);
    }

    private string? ResolveRedirectUrl(ResultExecutingContext context)
    {
        var urlHelper = _urlHelperFactory.GetUrlHelper(context);

        return context.Result switch
        {
            RedirectResult redirect => redirect.Url,
            LocalRedirectResult localRedirect => localRedirect.Url,
            RedirectToActionResult redirectToAction => AppendFragment(
                urlHelper.Action(
                    redirectToAction.ActionName,
                    redirectToAction.ControllerName,
                    redirectToAction.RouteValues),
                redirectToAction.Fragment),
            RedirectToRouteResult redirectToRoute => AppendFragment(
                urlHelper.RouteUrl(
                    redirectToRoute.RouteName,
                    redirectToRoute.RouteValues),
                redirectToRoute.Fragment),
            _ => null
        };
    }

    private static string? AppendFragment(string? url, string? fragment)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(fragment))
        {
            return url;
        }

        var normalizedFragment = fragment.StartsWith('#') ? fragment : $"#{fragment}";
        return $"{url}{normalizedFragment}";
    }
}
