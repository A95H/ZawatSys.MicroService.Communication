using MediatR;
using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Application.Commands.DispatchProviderWebhookToNormalization;

public sealed class DispatchProviderWebhookToNormalizationCommandHandler
    : IRequestHandler<DispatchProviderWebhookToNormalizationCommand, Unit>
{
    private readonly IProviderWebhookNormalizationPipeline _normalizationPipeline;

    public DispatchProviderWebhookToNormalizationCommandHandler(IProviderWebhookNormalizationPipeline normalizationPipeline)
    {
        _normalizationPipeline = normalizationPipeline;
    }

    public async Task<Unit> Handle(DispatchProviderWebhookToNormalizationCommand request, CancellationToken cancellationToken)
    {
        await _normalizationPipeline.ProcessAsync(request.Envelope, cancellationToken);
        return Unit.Value;
    }
}
