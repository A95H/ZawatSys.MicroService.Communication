namespace ZawatSys.MicroService.Communication.Application.Webhooks;

public sealed class ProviderWebhookReplayRejectedException : Exception
{
    public ProviderWebhookReplayRejectedException(string message)
        : base(message)
    {
    }
}
