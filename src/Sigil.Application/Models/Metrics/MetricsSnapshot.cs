using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Metrics;

/// <summary>Issue count for one project, status and level combination.</summary>
public sealed record IssueCountSample(string Project, IssueStatus Status, Severity Level, long Count);

/// <summary>Event count for one project and level combination inside the scrape window.</summary>
public sealed record EventCountSample(string Project, Severity Level, long Count);

/// <summary>Alert delivery count for one channel type and delivery status.</summary>
public sealed record AlertDeliverySample(AlertChannelType Channel, AlertDeliveryStatus Status, long Count);

/// <summary>Full set of values exposed by a single scrape.</summary>
/// <param name="Issues">Issue counts grouped by project, status and level.</param>
/// <param name="Events">Event counts grouped by project and level, over <paramref name="EventWindow"/>.</param>
/// <param name="AlertDeliveries">Cumulative alert deliveries grouped by channel type and status.</param>
/// <param name="EventsIngested">Events accepted into the ingestion pipeline since process start.</param>
/// <param name="EventsDropped">Events discarded by the digestion pipeline since process start.</param>
/// <param name="DigestionBacklog">Envelopes currently awaiting digestion.</param>
/// <param name="LastIngestionUtc">Most recent accepted ingestion instant, or null when none has been accepted.</param>
/// <param name="BuildVersion">Version of the running Sigil build.</param>
/// <param name="EventWindow">Window that <paramref name="Events"/> covers.</param>
public sealed record MetricsSnapshot(
    IReadOnlyList<IssueCountSample> Issues,
    IReadOnlyList<EventCountSample> Events,
    IReadOnlyList<AlertDeliverySample> AlertDeliveries,
    long EventsIngested,
    long EventsDropped,
    long DigestionBacklog,
    DateTime? LastIngestionUtc,
    string BuildVersion,
    TimeSpan EventWindow);

/// <summary>Defaults for the metrics endpoint.</summary>
public static class MetricsDefaults
{
    /// <summary>Window that sigil_events_total covers.</summary>
    public static readonly TimeSpan EventWindow = TimeSpan.FromHours(24);
}
