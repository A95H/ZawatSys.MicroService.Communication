using Microsoft.AspNetCore.Mvc;

namespace ZawatSys.MicroService.Communication.Api.Contracts.Webhooks;

public sealed class ProviderWebhookVerificationRequest
{
    [FromQuery(Name = "hub.mode")]
    public string? Mode { get; init; }

    [FromQuery(Name = "hub.verify_token")]
    public string? VerifyToken { get; init; }

    [FromQuery(Name = "hub.challenge")]
    public string? Challenge { get; init; }
}
