namespace ZawatSys.MicroService.Communication.Infrastructure.Outbound;

internal static class OutboundRetryPolicy
{
    public static bool CanRetry(int attemptNumber, OutboundRetryOptions options)
    {
        var maxAttempts = Math.Max(1, options.MaxAttempts);
        return attemptNumber < maxAttempts;
    }

    public static TimeSpan CalculateDelay(int attemptNumber, TimeSpan? providerRetryAfter, OutboundRetryOptions options)
    {
        var baseDelay = options.BaseDelay <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : options.BaseDelay;
        var maxDelay = options.MaxDelay < baseDelay ? baseDelay : options.MaxDelay;
        var exponent = Math.Max(0, attemptNumber - 1);
        var exponentialDelay = TimeSpan.FromTicks((long)Math.Min(maxDelay.Ticks, baseDelay.Ticks * Math.Pow(2d, exponent)));
        var providerDelay = providerRetryAfter.GetValueOrDefault();

        if (providerDelay <= TimeSpan.Zero)
        {
            return exponentialDelay;
        }

        return providerDelay > maxDelay
            ? maxDelay
            : providerDelay > exponentialDelay
                ? providerDelay
                : exponentialDelay;
    }
}
