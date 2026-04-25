using System.Diagnostics;

namespace ZawatSys.MicroService.Communication.Infrastructure.Observability;

internal static class CommunicationObservability
{
    public static double ToMilliseconds(TimeSpan duration)
        => duration < TimeSpan.Zero ? 0d : duration.TotalMilliseconds;

    public static double ToMilliseconds(DateTimeOffset start, DateTimeOffset end)
        => ToMilliseconds(end - start);

    public static string GetAttemptKind(int attemptNumber)
        => attemptNumber > 1 ? "retry" : "initial";

    public static string GetHttpStatusClass(int? httpStatusCode)
    {
        if (!httpStatusCode.HasValue)
        {
            return "none";
        }

        return httpStatusCode.Value switch
        {
            >= 100 and < 200 => "1xx",
            >= 200 and < 300 => "2xx",
            >= 300 and < 400 => "3xx",
            >= 400 and < 500 => "4xx",
            >= 500 and < 600 => "5xx",
            _ => "other"
        };
    }

    public static void SetCommonConversationTags(
        Activity? activity,
        Guid tenantId,
        Guid conversationId,
        Guid sessionId,
        Guid correlationId)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("tenant.id", tenantId);
        activity.SetTag("conversation.id", conversationId);
        activity.SetTag("session.id", sessionId);
        activity.SetTag("correlation.id", correlationId == Guid.Empty ? null : correlationId.ToString("D"));
    }
}
