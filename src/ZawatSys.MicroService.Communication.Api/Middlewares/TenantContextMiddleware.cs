using ZawatSys.MicroService.Communication.Api.Services;

namespace ZawatSys.MicroService.Communication.Api.Middlewares;

/// <summary>
/// Captures correlation and tenant context for each request.
/// </summary>
public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    public TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationHeaderValue = context.Request.Headers[TenantContextConstants.CorrelationIdHeader].FirstOrDefault();
        if (!Guid.TryParse(correlationHeaderValue, out var parsedCorrelationId))
        {
            if (!string.IsNullOrWhiteSpace(correlationHeaderValue))
            {
                _logger.LogWarning("Ignoring malformed correlation header value: {CorrelationHeaderValue}", correlationHeaderValue);
            }

            parsedCorrelationId = Guid.NewGuid();
        }

        var normalizedCorrelationId = parsedCorrelationId.ToString("D");
        context.Items[TenantContextConstants.CorrelationIdItemKey] = normalizedCorrelationId;
        context.Items[TenantContextConstants.CorrelationIdHeader] = normalizedCorrelationId;
        context.Response.Headers[TenantContextConstants.CorrelationIdHeader] = normalizedCorrelationId;

        var tenantIdValue = context.Request.Headers[TenantContextConstants.TenantIdHeader].FirstOrDefault();

        if (!Guid.TryParse(tenantIdValue, out var normalizedTenantId))
        {
            if (!string.IsNullOrWhiteSpace(tenantIdValue))
            {
                _logger.LogWarning("Ignoring malformed tenant header value: {TenantHeaderValue}", tenantIdValue);
            }

            var tenantClaimValue = context.User.FindFirst(TenantContextConstants.TenantIdClaimType)?.Value
                                   ?? context.User.FindFirst(TenantContextConstants.TenantIdClaimTypeAlt)?.Value
                                   ?? context.User.FindFirst(TenantContextConstants.TenantIdClaimTypeAlt2)?.Value;

            if (Guid.TryParse(tenantClaimValue, out normalizedTenantId))
            {
                context.Items[TenantContextConstants.TenantIdItemKey] = normalizedTenantId.ToString("D");
            }
            else if (!string.IsNullOrWhiteSpace(tenantClaimValue))
            {
                _logger.LogWarning("Ignoring malformed tenant claim value: {TenantClaimValue}", tenantClaimValue);
            }

            await _next(context);
            return;
        }

        context.Items[TenantContextConstants.TenantIdItemKey] = normalizedTenantId.ToString("D");

        await _next(context);
    }
}

public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantContextMiddleware>();
    }
}
