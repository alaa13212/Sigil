using Microsoft.Extensions.Hosting;
using Sigil.Application.Interfaces;

namespace Sigil.infrastructure.Workers;

internal class WorkersHost(IEnumerable<IWorker> workers) : IHostedService
{
    private readonly List<Task> _runningTasks = [];
    private readonly CancellationTokenSource _cts = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var worker in workers)
        {
            var task = worker.RunAsync(_cts.Token);
            _runningTasks.Add(task);
        }

        return Task.CompletedTask;
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