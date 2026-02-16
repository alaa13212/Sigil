namespace Sigil.Application.Interfaces;

public interface IWorker
{
    Task RunAsync(CancellationToken stoppingToken = default);
}

public interface IWorker<in TItem> : IWorker
{
    bool TryEnqueue(TItem item);
}