namespace ZawatSys.MicroService.Communication.Infrastructure.Outbound;

public sealed class OutboundRetryOptions
{
    public const string SectionName = "OutboundRetry";

    public int MaxAttempts { get; set; } = 5;

    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(30);
}
