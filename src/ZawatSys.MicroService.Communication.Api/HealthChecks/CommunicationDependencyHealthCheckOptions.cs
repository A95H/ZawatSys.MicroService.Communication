namespace ZawatSys.MicroService.Communication.Api.HealthChecks;

public sealed class CommunicationDependencyHealthCheckOptions
{
    public const string SectionName = "HealthChecks";

    public int DependencyTimeoutSeconds { get; set; } = 5;

    public TimeSpan GetDependencyTimeout()
        => TimeSpan.FromSeconds(Math.Max(1, DependencyTimeoutSeconds));
}
