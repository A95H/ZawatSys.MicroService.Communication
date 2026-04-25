using System.Collections.Concurrent;

namespace ZawatSys.MicroService.Communication.Infrastructure.Webhooks;

internal static class InboundWebhookLockCoordinator
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.Ordinal);

    public static async Task<IAsyncDisposable> AcquireBindingAsync(
        Guid tenantId,
        Guid endpointId,
        string normalizedExternalUserId,
        CancellationToken cancellationToken)
    {
        return await AcquireAsync($"binding:{tenantId:D}:{endpointId:D}:{normalizedExternalUserId}", cancellationToken);
    }

    public static async Task<IAsyncDisposable> AcquireSessionAsync(
        Guid tenantId,
        Guid endpointId,
        Guid bindingId,
        CancellationToken cancellationToken)
    {
        return await AcquireAsync($"session:{tenantId:D}:{endpointId:D}:{bindingId:D}", cancellationToken);
    }

    private static async Task<IAsyncDisposable> AcquireAsync(string key, CancellationToken cancellationToken)
    {
        var semaphore = Locks.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public Releaser(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public ValueTask DisposeAsync()
        {
            _semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
