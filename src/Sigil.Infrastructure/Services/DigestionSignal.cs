using System.Threading.Channels;
using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Services;

internal class DigestionSignal : IDigestionSignal
{
    // Stryker disable once ObjectInitializer : FullMode.DropWrite vs default only affects WriteAsync; Signal() uses TryWrite which returns false when full regardless of mode
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    // Stryker disable once Boolean : the written value (true) is never read by the consumer; only the presence of an item matters
    public void Signal() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await _channel.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // timeout — normal, continue processing
        }
    }
}
