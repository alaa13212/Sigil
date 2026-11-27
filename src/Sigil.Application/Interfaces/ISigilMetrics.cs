namespace Sigil.Application.Interfaces;

/// <summary>
/// Process-local counters for the Prometheus metrics endpoint. All values reset on restart.
/// </summary>
public interface ISigilMetrics
{
    /// <summary>Adds to the running count of events accepted into the ingestion pipeline.</summary>
    void RecordEventsIngested(int count);

    /// <summary>Adds to the running count of events discarded by the digestion pipeline.</summary>
    void RecordEventsDropped(int count);

    /// <summary>Adds to the running count of envelopes discarded for one envelope item type.</summary>
    void RecordEnvelopeDiscarded(string type);

    /// <summary>Records the UTC instant of the most recent accepted ingestion batch.</summary>
    void RecordLastIngestion(DateTime receivedAtUtc);

    /// <summary>Reads the current value of every process-local counter.</summary>
    SigilMetricsSnapshot GetSnapshot();
}

/// <summary>Counter values held by <see cref="ISigilMetrics"/>.</summary>
/// <param name="EventsIngested">Events accepted into the ingestion pipeline since process start.</param>
/// <param name="EventsDropped">Events discarded by the digestion pipeline since process start.</param>
/// <param name="LastIngestionUtc">Most recent accepted ingestion instant, or null when none has been accepted.</param>
/// <param name="EnvelopesDiscarded">Envelopes discarded since process start, by envelope item type.</param>
public record SigilMetricsSnapshot(
    long EventsIngested,
    long EventsDropped,
    DateTime? LastIngestionUtc,
    IReadOnlyDictionary<string, long> EnvelopesDiscarded);
