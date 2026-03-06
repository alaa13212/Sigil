using Sigil.Domain.Extensions;

namespace Sigil.Domain.Tests.Extensions;

public class SemaphoreSlimExtensionTests
{
    [Fact]
    public async Task LockAsync_AcquiresLock_CountDropsToZero()
    {
        var semaphore = new SemaphoreSlim(1, 1);

        var releaser = await semaphore.LockAsync();

        semaphore.CurrentCount.Should().Be(0);
        releaser.Dispose();
    }

    [Fact]
    public async Task LockAsync_Dispose_RestoresCount()
    {
        var semaphore = new SemaphoreSlim(1, 1);

        var releaser = await semaphore.LockAsync();
        releaser.Dispose();

        semaphore.CurrentCount.Should().Be(1);
    }

    [Fact]
    public async Task LockAsync_CancellationToken_ThrowsWhenCancelled()
    {
        var semaphore = new SemaphoreSlim(0, 1); // starts with 0 — already locked
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var act = async () => await semaphore.LockAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
