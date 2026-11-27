using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Services;

/// <summary>Process-local counter store for the metrics endpoint. Counters reset on restart.</summary>
internal sealed class SigilMetrics : ISigilMetrics
{
    private long _eventsIngested;
    private long _eventsDropped;
    private long _lastIngestionTicks = DateTime.MinValue.Ticks;

    public void RecordEventsIngested(int count)
    {
        if (count > 0)
            Interlocked.Add(ref _eventsIngested, count);
    }

    public void RecordEventsDropped(int count)
    {
        if (count > 0)
            Interlocked.Add(ref _eventsDropped, count);
    }

    public void RecordLastIngestion(DateTime receivedAtUtc)
    {
        long ticks = receivedAtUtc.Kind == DateTimeKind.Utc
            ? receivedAtUtc.Ticks
            : receivedAtUtc.ToUniversalTime().Ticks;

        long observed = Volatile.Read(ref _lastIngestionTicks);
        while (ticks > observed)
        {
            long original = Interlocked.CompareExchange(ref _lastIngestionTicks, ticks, observed);
            if (original == observed)
                return;

            observed = original;
        }
    }

    public SigilMetricsSnapshot GetSnapshot()
    {
        long ticks = Volatile.Read(ref _lastIngestionTicks);
        DateTime? lastIngestion = ticks == DateTime.MinValue.Ticks
            ? null
            : new DateTime(ticks, DateTimeKind.Utc);

        return new SigilMetricsSnapshot(
            Interlocked.Read(ref _eventsIngested),
            Interlocked.Read(ref _eventsDropped),
            lastIngestion);
    }
}
