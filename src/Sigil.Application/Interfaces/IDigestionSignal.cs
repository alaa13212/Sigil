namespace Sigil.Application.Interfaces;

public interface IDigestionSignal
{
    void Signal();
    Task WaitAsync(TimeSpan timeout, CancellationToken ct);
}
