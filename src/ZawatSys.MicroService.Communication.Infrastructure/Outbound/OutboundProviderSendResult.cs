namespace ZawatSys.MicroService.Communication.Infrastructure.Outbound;

public sealed record OutboundProviderSendResult(
    bool Accepted,
    int? HttpStatusCode,
    string? ProviderMessageId,
    string? ResponseCode,
    string? ErrorCode,
    string? ErrorMessageRedacted,
    IReadOnlyDictionary<string, object?>? ResponseMetadata = null,
    TimeSpan? RetryAfter = null);
