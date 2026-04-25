using ZawatSys.MicroService.Communication.Application.Webhooks;

namespace ZawatSys.MicroService.Communication.Api.Routing;

public static class ProviderWebhookRouting
{
    public const string RouteConstraintName = "supportedWebhookProvider";
    public const string MetaProvider = ProviderWebhookProviders.Meta;
    public const string TelegramProvider = ProviderWebhookProviders.Telegram;

    public static string GetVerificationContentType(string provider)
    {
        return provider switch
        {
            MetaProvider => "text/plain",
            _ => "text/plain"
        };
    }

    public static string GetVerificationResponseBody(string provider, string challenge)
    {
        return provider switch
        {
            MetaProvider => challenge,
            _ => challenge
        };
    }

    public static string GetCallbackContentType(string provider)
    {
        return provider switch
        {
            MetaProvider => "text/plain",
            _ => "text/plain"
        };
    }

    public static string GetCallbackResponseBody(string provider)
    {
        return provider switch
        {
            MetaProvider => "EVENT_RECEIVED",
            TelegramProvider => string.Empty,
            _ => string.Empty
        };
    }
}
