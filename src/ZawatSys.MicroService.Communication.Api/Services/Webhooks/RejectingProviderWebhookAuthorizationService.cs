using Microsoft.Extensions.Logging;

namespace ZawatSys.MicroService.Communication.Api.Services.Webhooks;

public sealed class RejectingProviderWebhookAuthorizationService : IProviderWebhookAuthorizationService
{
    private readonly ILogger<RejectingProviderWebhookAuthorizationService> _logger;

    public RejectingProviderWebhookAuthorizationService(ILogger<RejectingProviderWebhookAuthorizationService> logger)
    {
        _logger = logger;
    }

    public Task<bool> AuthorizeAsync(ProviderWebhookAuthorizationRequest request, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        _logger.LogWarning(
            "Rejecting webhook request because provider authorization chain is not configured yet. Provider: {Provider}, EndpointKey: {EndpointKey}, EventType: {EventType}",
            request.Provider,
            request.EndpointKey,
            request.EventType);

        return Task.FromResult(false);
    }
}
