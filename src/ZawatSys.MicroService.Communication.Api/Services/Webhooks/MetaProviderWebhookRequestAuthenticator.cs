using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZawatSys.MicroService.Communication.Api.Routing;
using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Api.Services.Webhooks;

public sealed class MetaProviderWebhookRequestAuthenticator : IProviderWebhookRequestAuthenticator
{
    private const string SignatureHeaderName = "X-Hub-Signature-256";

    private readonly IConfiguration _configuration;
    private readonly ILogger<MetaProviderWebhookRequestAuthenticator> _logger;

    public MetaProviderWebhookRequestAuthenticator(
        IConfiguration configuration,
        ILogger<MetaProviderWebhookRequestAuthenticator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool CanHandle(string provider)
    {
        return string.Equals(provider, ProviderWebhookRouting.MetaProvider, StringComparison.OrdinalIgnoreCase);
    }

    public Task<bool> AuthorizeAsync(ProviderWebhookAuthorizationRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        return Task.FromResult(request.EventType switch
        {
            ProviderWebhookEventTypes.Verification => AuthorizeVerification(request),
            ProviderWebhookEventTypes.Inbound or ProviderWebhookEventTypes.DeliveryStatus => AuthorizeSignedCallback(request),
            _ => false
        });
    }

    private bool AuthorizeVerification(ProviderWebhookAuthorizationRequest request)
    {
        var configuredToken = GetEndpointSetting(request.EndpointKey, "VerifyToken");
        var providedToken = request.HttpContext.Request.Query["hub.verify_token"].ToString();

        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            _logger.LogWarning(
                "Rejecting Meta verification webhook because VerifyToken is missing. EndpointKey: {EndpointKey}",
                request.EndpointKey);
            return false;
        }

        return string.Equals(configuredToken, providedToken, StringComparison.Ordinal);
    }

    private bool AuthorizeSignedCallback(ProviderWebhookAuthorizationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RawBody))
        {
            _logger.LogWarning(
                "Rejecting Meta webhook because raw payload is missing. EndpointKey: {EndpointKey}, EventType: {EventType}",
                request.EndpointKey,
                request.EventType);
            return false;
        }

        var appSecret = GetEndpointSetting(request.EndpointKey, "AppSecret");
        if (string.IsNullOrWhiteSpace(appSecret))
        {
            _logger.LogWarning(
                "Rejecting Meta webhook because AppSecret is missing. EndpointKey: {EndpointKey}, EventType: {EventType}",
                request.EndpointKey,
                request.EventType);
            return false;
        }

        if (!request.HttpContext.Request.Headers.TryGetValue(SignatureHeaderName, out var signatureValues))
        {
            _logger.LogWarning(
                "Rejecting Meta webhook because signature header is missing. EndpointKey: {EndpointKey}, EventType: {EventType}",
                request.EndpointKey,
                request.EventType);
            return false;
        }

        var providedSignature = signatureValues.ToString().Trim();
        if (!providedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Rejecting Meta webhook because signature format is invalid. EndpointKey: {EndpointKey}, EventType: {EventType}",
                request.EndpointKey,
                request.EventType);
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(request.RawBody));
        var expectedSignature = $"sha256={Convert.ToHexStringLower(hash)}";

        return FixedTimeEquals(expectedSignature, providedSignature);
    }

    private string? GetEndpointSetting(string endpointKey, string settingName)
    {
        return _configuration[$"Webhooks:Providers:Meta:Endpoints:{endpointKey}:{settingName}"];
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
