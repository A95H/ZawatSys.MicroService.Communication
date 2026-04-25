namespace ZawatSys.MicroService.Communication.Application.Webhooks;

public sealed record ProviderWebhookEnvelope(
    string Provider,
    string EndpointKey,
    string EventType,
    string Method,
    string Path,
    string? ContentType,
    string? RawBody,
    IReadOnlyDictionary<string, string[]> Headers,
    IReadOnlyDictionary<string, string[]> Query);
