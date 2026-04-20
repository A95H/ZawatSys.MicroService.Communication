using System.Text;
using System.Text.Json;
using ZawatSys.MicroService.Communication.Api.Contracts;
using ZawatSys.MicroService.Communication.Api.Exceptions;
using ZawatSys.MicroService.Communication.Api.Services;
using ZawatSys.MicroLib.Shared.Contracts.Responses;

namespace ZawatSys.MicroService.Communication.Api.Middlewares;

/// <summary>
/// Global middleware that converts exceptions to deterministic API error envelopes.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IExceptionMapper _exceptionMapper;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IExceptionMapper exceptionMapper)
    {
        _next = next;
        _logger = logger;
        _exceptionMapper = exceptionMapper;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var mapped = _exceptionMapper.Map(ex);

            _logger.LogError(
                ex,
                "Unhandled exception mapped to {StatusCode} / {MsgCode} for {Path}",
                mapped.StatusCode,
                mapped.MsgCode,
                context.Request.Path);

            var correlationId = context.Items.TryGetValue(TenantContextConstants.CorrelationIdItemKey, out var cidValue)
                ? cidValue?.ToString()
                : context.TraceIdentifier;

            var payload = new ApiErrorEnvelope
            {
                IsSuccess = false,
                MsgCode = mapped.MsgCode,
                Data = null,
                Error = new InternalError
                {
                    Title = mapped.Title,
                    Details = string.IsNullOrWhiteSpace(correlationId)
                        ? mapped.Details
                        : $"{mapped.Details} [correlationId:{correlationId}]",
                    Errors = mapped.Errors.ToList()
                }
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = mapped.StatusCode;

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8);
        }
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
