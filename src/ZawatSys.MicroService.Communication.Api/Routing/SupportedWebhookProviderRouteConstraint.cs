using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ZawatSys.MicroService.Communication.Api.Routing;

public sealed class SupportedWebhookProviderRouteConstraint : IRouteConstraint
{
    private static readonly HashSet<string> SupportedProviders =
    [
        ProviderWebhookRouting.MetaProvider,
        ProviderWebhookRouting.TelegramProvider
    ];

    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        _ = httpContext;
        _ = route;
        _ = routeDirection;

        if (!values.TryGetValue(routeKey, out var candidate) || candidate is null)
        {
            return false;
        }

        return SupportedProviders.Contains(candidate.ToString() ?? string.Empty);
    }
}
