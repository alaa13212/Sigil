using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Workers;

internal class WorkersHost(
    IEnumerable<IWorker> workers,
    ILogger<WorkersHost> logger) : IHostedService
{
    private readonly List<Task> _runningTasks = [];
    private readonly CancellationTokenSource _cts = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var worker in workers)
        {
            var task = RunWithRestartAsync(worker);
            _runningTasks.Add(task);
        }

        return Task.CompletedTask;
    }

    private async Task RunWithRestartAsync(IWorker worker)
    {
        var workerName = worker.GetType().Name;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await worker.RunAsync(_cts.Token);
                break; // Clean exit
            }
            catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Worker {Worker} crashed unexpectedly. Restarting after delay", workerName);
                try { await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _cts.CancelAsync();

        try
        {
            await Task.WhenAll(_runningTasks);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown
        }
    }
}
