namespace ZawatSys.MicroService.Communication.Api.Services.Webhooks;

public sealed record ProviderWebhookAuthorizationRequest(
    string Provider,
    string EndpointKey,
    string EventType,
    HttpContext HttpContext,
    string? RawBody);
