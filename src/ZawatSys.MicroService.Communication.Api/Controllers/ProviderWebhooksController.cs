using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZawatSys.MicroService.Communication.Api.Contracts.Webhooks;
using ZawatSys.MicroService.Communication.Api.Routing;
using ZawatSys.MicroService.Communication.Api.Services.Webhooks;
using ZawatSys.MicroService.Communication.Application.Commands.DispatchProviderWebhookToNormalization;
using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/providers/{provider:" + ProviderWebhookRouting.RouteConstraintName + "}/{endpointKey}/webhooks")]
public sealed class ProviderWebhooksController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IProviderWebhookAuthorizationService _authorizationService;

    public ProviderWebhooksController(
        ISender sender,
        IProviderWebhookAuthorizationService authorizationService)
    {
        _sender = sender;
        _authorizationService = authorizationService;
    }

    [HttpGet("verification")]
    public async Task<IActionResult> VerifyAsync(
        [FromRoute] string provider,
        [FromRoute] string endpointKey,
        [FromQuery] ProviderWebhookVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Mode, "subscribe", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(request.VerifyToken)
            || string.IsNullOrWhiteSpace(request.Challenge))
        {
            throw new ArgumentException("Provider verification request is invalid.");
        }

        await AuthorizeAsync(provider, endpointKey, ProviderWebhookEventTypes.Verification, rawBody: null, cancellationToken);
        await DispatchToNormalizationAsync(provider, endpointKey, ProviderWebhookEventTypes.Verification, rawBody: null, cancellationToken);

        return Content(
            ProviderWebhookRouting.GetVerificationResponseBody(provider, request.Challenge),
            ProviderWebhookRouting.GetVerificationContentType(provider),
            Encoding.UTF8);
    }

    [HttpPost("inbound")]
    public async Task<IActionResult> ReceiveInboundAsync(
        [FromRoute] string provider,
        [FromRoute] string endpointKey,
        CancellationToken cancellationToken)
    {
        var rawBody = await ReadRequestBodyAsync(cancellationToken);

        await AuthorizeAsync(provider, endpointKey, ProviderWebhookEventTypes.Inbound, rawBody, cancellationToken);
        await DispatchToNormalizationAsync(provider, endpointKey, ProviderWebhookEventTypes.Inbound, rawBody, cancellationToken);

        return Content(
            ProviderWebhookRouting.GetCallbackResponseBody(provider),
            ProviderWebhookRouting.GetCallbackContentType(provider),
            Encoding.UTF8);
    }

    [HttpPost("delivery-status")]
    public async Task<IActionResult> ReceiveDeliveryStatusAsync(
        [FromRoute] string provider,
        [FromRoute] string endpointKey,
        CancellationToken cancellationToken)
    {
        var rawBody = await ReadRequestBodyAsync(cancellationToken);

        await AuthorizeAsync(provider, endpointKey, ProviderWebhookEventTypes.DeliveryStatus, rawBody, cancellationToken);
        await DispatchToNormalizationAsync(provider, endpointKey, ProviderWebhookEventTypes.DeliveryStatus, rawBody, cancellationToken);

        return Content(
            ProviderWebhookRouting.GetCallbackResponseBody(provider),
            ProviderWebhookRouting.GetCallbackContentType(provider),
            Encoding.UTF8);
    }

    private async Task AuthorizeAsync(
        string provider,
        string endpointKey,
        string eventType,
        string? rawBody,
        CancellationToken cancellationToken)
    {
        var isAuthorized = await _authorizationService.AuthorizeAsync(
            new ProviderWebhookAuthorizationRequest(provider, endpointKey, eventType, HttpContext, rawBody),
            cancellationToken);

        if (!isAuthorized)
        {
            throw new UnauthorizedAccessException("Webhook request authorization failed.");
        }
    }

    private async Task DispatchToNormalizationAsync(
        string provider,
        string endpointKey,
        string eventType,
        string? rawBody,
        CancellationToken cancellationToken)
    {
        var envelope = new ProviderWebhookEnvelope(
            provider,
            endpointKey,
            eventType,
            Request.Method,
            Request.Path,
            Request.ContentType,
            rawBody,
            Request.Headers.ToDictionary(header => header.Key, header => header.Value.Select(static value => value ?? string.Empty).ToArray(), StringComparer.OrdinalIgnoreCase),
            Request.Query.ToDictionary(query => query.Key, query => query.Value.Select(static value => value ?? string.Empty).ToArray(), StringComparer.OrdinalIgnoreCase));

        await _sender.Send(new DispatchProviderWebhookToNormalizationCommand(envelope), cancellationToken);
    }

    private async Task<string> ReadRequestBodyAsync(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        return rawBody;
    }
}
