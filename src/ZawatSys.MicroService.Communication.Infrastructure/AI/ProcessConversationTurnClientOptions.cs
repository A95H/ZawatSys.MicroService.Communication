namespace ZawatSys.MicroService.Communication.Infrastructure.AI;

public sealed class ProcessConversationTurnClientOptions
{
    public const string SectionName = "AI:ProcessConversationTurn";

    public int TimeoutSeconds { get; set; } = 15;

    public int RetryCount { get; set; } = 2;

    public int RetryDelayMilliseconds { get; set; } = 200;
}
