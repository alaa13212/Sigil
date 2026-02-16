namespace Sigil.Domain.Extensions;

public static class SemaphoreSlimExtensions
{
    public static async Task<IDisposable> LockAsync(this SemaphoreSlim semaphore, CancellationToken cancellationToken = default)
    {
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private readonly struct Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        public void Dispose() => semaphore.Release();
    }
}