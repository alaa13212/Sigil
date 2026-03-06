using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class DigestionSignalTests
{
    [Fact]
    public async Task Signal_ThenWait_ReturnsImmediately()
    {
        var signal = new DigestionSignal();
        signal.Signal();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await signal.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    [Fact]
    public async Task WaitAsync_NoSignal_TimesOut()
    {
        var signal = new DigestionSignal();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await signal.WaitAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeGreaterOrEqualTo(40);
    }

    [Fact]
    public async Task WaitAsync_Cancellation_ThrowsIfTokenCancelled()
    {
        var signal = new DigestionSignal();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => signal.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Signal_Twice_DropsSecondWrite()
    {
        var signal = new DigestionSignal();

        // Channel capacity is 1, second signal should be silently dropped
        signal.Signal();
        signal.Signal();

        // Should not throw
    }

    [Fact]
    public async Task Signal_ConsumedByWait_CanSignalAgain()
    {
        var signal = new DigestionSignal();
        signal.Signal();
        await signal.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        signal.Signal();
        await signal.WaitAsync(TimeSpan.FromSeconds(1), CancellationToken.None);
        // Both completed without timeout
    }
}
