using MediatR;
using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Application.Commands.DispatchProviderWebhookToNormalization;

public sealed record DispatchProviderWebhookToNormalizationCommand(
    ProviderWebhookEnvelope Envelope) : IRequest<Unit>;
