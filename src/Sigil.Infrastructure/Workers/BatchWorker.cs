using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Sigil.Application.Interfaces;

namespace Sigil.infrastructure.Workers;

internal abstract class BatchWorker<TItem> : IWorker<TItem>
{
    private readonly int _batchSize;
    private readonly TimeSpan _flushTimeout;
    private readonly Channel<TItem> _channel;
    private readonly ILogger _logger;

    protected BatchWorker(BatchWorkerOptions options, ILogger logger)
    {
        _batchSize = options.BatchSize;
        _flushTimeout = options.FlushTimeout;
        _logger = logger;

        if (options.Cap is > 0)
            _channel = Channel.CreateBounded<TItem>(new BoundedChannelOptions(options.Cap.Value)
            {
                SingleReader = true,
                AllowSynchronousContinuations = false
            });
        else
            _channel = Channel.CreateUnbounded<TItem>(new UnboundedChannelOptions
            {
                SingleReader = true,
                AllowSynchronousContinuations = false
            });
    }
    
    

    public bool TryEnqueue(TItem item) => _channel.Writer.TryWrite(item);

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        var buffer = new List<TItem>(_batchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var batchTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            batchTimeoutCts.CancelAfter(_flushTimeout);

            try
            {
                while (buffer.Count < _batchSize)
                {
                    var item = await _channel.Reader.ReadAsync(batchTimeoutCts.Token);
                    buffer.Add(item);
                }

                await ProcessAndClearBatchAsync();
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                if (buffer.Count > 0)
                {
                    await ProcessAndClearBatchAsync();
                }
            }
            catch (ChannelClosedException)
            {
                if (buffer.Count > 0)
                {
                    await ProcessAndClearBatchAsync();
                }
                break;
            }
        }

        // Final flush on shutdown - process any remaining items
        if (buffer.Count > 0)
        {
            await ProcessAndClearBatchAsync();
        }

        async Task ProcessAndClearBatchAsync()
        {
            try
            {
                await ProcessBatchAsync(buffer, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch of {Count} items. Batch will be discarded to prevent worker from stopping", buffer.Count);
            }
            finally
            {
                buffer.Clear();
            }
        }
    }

    protected abstract Task ProcessBatchAsync(List<TItem> items, CancellationToken ct);
}