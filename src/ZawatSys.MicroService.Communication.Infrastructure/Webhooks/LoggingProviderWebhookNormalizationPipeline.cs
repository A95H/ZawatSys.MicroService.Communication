using Microsoft.Extensions.Logging;
using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

public sealed class LoggingProviderWebhookNormalizationPipeline : IProviderWebhookNormalizationPipeline
{
    private readonly IReadOnlyList<IProviderWebhookPayloadNormalizer> _normalizers;
    private readonly IInboundWebhookIngestionService _inboundWebhookIngestionService;
    private readonly IDeliveryStatusWebhookReconciliationService _deliveryStatusWebhookReconciliationService;
    private readonly ILogger<LoggingProviderWebhookNormalizationPipeline> _logger;

    public LoggingProviderWebhookNormalizationPipeline(
        IEnumerable<IProviderWebhookPayloadNormalizer> normalizers,
        IInboundWebhookIngestionService inboundWebhookIngestionService,
        IDeliveryStatusWebhookReconciliationService deliveryStatusWebhookReconciliationService,
        ILogger<LoggingProviderWebhookNormalizationPipeline> logger)
    {
        _normalizers = normalizers.ToList();
        _inboundWebhookIngestionService = inboundWebhookIngestionService;
        _deliveryStatusWebhookReconciliationService = deliveryStatusWebhookReconciliationService;
        _logger = logger;
    }

    public async Task ProcessAsync(ProviderWebhookEnvelope envelope, CancellationToken cancellationToken)
    {
        var normalizer = _normalizers.FirstOrDefault(candidate => candidate.CanNormalize(envelope.Provider, envelope.EventType))
            ?? throw new InvalidOperationException($"No webhook normalizer is registered for provider '{envelope.Provider}' and event type '{envelope.EventType}'.");

        var normalizedWebhook = normalizer.Normalize(envelope);

        _logger.LogInformation(
            "Normalized provider webhook. Provider: {Provider}, EndpointKey: {EndpointKey}, EventType: {EventType}, ProviderObject: {ProviderObject}, EntryCount: {EntryCount}, RawPayloadSha256: {RawPayloadSha256}",
            normalizedWebhook.Provider,
            normalizedWebhook.EndpointKey,
            normalizedWebhook.EventType,
            normalizedWebhook.ProviderObject,
            normalizedWebhook.Entries.Count,
            normalizedWebhook.RawPayloadReference.Sha256);

        if (string.Equals(normalizedWebhook.EventType, ProviderWebhookEventTypes.Inbound, StringComparison.Ordinal))
        {
            await _inboundWebhookIngestionService.IngestAsync(normalizedWebhook, cancellationToken);
            return;
        }

        if (string.Equals(normalizedWebhook.EventType, ProviderWebhookEventTypes.DeliveryStatus, StringComparison.Ordinal))
        {
            await _deliveryStatusWebhookReconciliationService.ReconcileAsync(normalizedWebhook, cancellationToken);
        }
    }
}
