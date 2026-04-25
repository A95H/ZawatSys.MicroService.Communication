using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroService.Communication.Api.Routing;
using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Api.Services.Webhooks;

public sealed class TelegramProviderWebhookRequestAuthenticator : IProviderWebhookRequestAuthenticator
{
    private const string SecretTokenHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramProviderWebhookRequestAuthenticator> _logger;

    public TelegramProviderWebhookRequestAuthenticator(
        IConfiguration configuration,
        ILogger<TelegramProviderWebhookRequestAuthenticator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool CanHandle(string provider)
    {
        return string.Equals(provider, ProviderWebhookRouting.TelegramProvider, StringComparison.OrdinalIgnoreCase);
    }

    public Task<bool> AuthorizeAsync(ProviderWebhookAuthorizationRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        return Task.FromResult(request.EventType switch
        {
            ProviderWebhookEventTypes.Inbound => AuthorizeInboundCallback(request),
            ProviderWebhookEventTypes.Verification => RejectUnsupportedEventType(request, "verification"),
            ProviderWebhookEventTypes.DeliveryStatus => RejectUnsupportedEventType(request, "delivery-status"),
            _ => false
        });
    }

    private bool AuthorizeInboundCallback(ProviderWebhookAuthorizationRequest request)
    {
        var configuredSecret = GetEndpointSetting(request.EndpointKey, "WebhookSecretToken");
        if (string.IsNullOrWhiteSpace(configuredSecret))
        {
            _logger.LogWarning(
                "Rejecting Telegram inbound webhook because WebhookSecretToken is missing. EndpointKey: {EndpointKey}",
                request.EndpointKey);
            return false;
        }

        if (!request.HttpContext.Request.Headers.TryGetValue(SecretTokenHeaderName, out var providedSecretValues))
        {
            _logger.LogWarning(
                "Rejecting Telegram inbound webhook because secret token header is missing. EndpointKey: {EndpointKey}",
                request.EndpointKey);
            return false;
        }

        var providedSecret = providedSecretValues.ToString().Trim();
        return FixedTimeEquals(configuredSecret.Trim(), providedSecret);
    }

    private bool RejectUnsupportedEventType(ProviderWebhookAuthorizationRequest request, string eventType)
    {
        _logger.LogInformation(
            "Rejecting Telegram webhook because event type is unsupported. EndpointKey: {EndpointKey}, EventType: {EventType}",
            request.EndpointKey,
            eventType);

        return false;
    }

    private string? GetEndpointSetting(string endpointKey, string settingName)
    {
        return _configuration[$"Webhooks:Providers:Telegram:Endpoints:{endpointKey}:{settingName}"];
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
