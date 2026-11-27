using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Metrics;

/// <summary>Issue count for one project, status and level combination.</summary>
public sealed record IssueCountSample(string Project, int ProjectId, IssueStatus Status, Severity Level, long Count);

/// <summary>Event count for one project, level, window, environment and issue status combination.</summary>
public sealed record EventCountSample(
    string Project,
    int ProjectId,
    Severity Level,
    TimeSpan Window,
    string Environment,
    IssueStatus Status,
    long Count);

/// <summary>Alert delivery count for one channel type and delivery status.</summary>
public sealed record AlertDeliverySample(AlertChannelType Channel, AlertDeliveryStatus Status, long Count);

/// <summary>Event count for one project and level inside one time window, without the environment and status breakdown.</summary>
public sealed record ProjectEventCountSample(
    string Project,
    int ProjectId,
    Severity Level,
    TimeSpan Window,
    long Count);

/// <summary>Count of issues first seen inside one time window.</summary>
public sealed record NewIssueCountSample(string Project, int ProjectId, Severity Level, TimeSpan Window, long Count);

/// <summary>Newest release observed for one project.</summary>
public sealed record ReleaseInfoSample(string Project, int ProjectId, string Release, string? Commit, DateTime FirstSeenAt);

/// <summary>Discarded envelope count for one envelope item type.</summary>
public sealed record EnvelopeDiscardSample(string Type, long Count);

/// <summary>Full set of values exposed by a single scrape.</summary>
/// <param name="Issues">Issue counts grouped by project, status and level.</param>
/// <param name="Events">Event counts grouped by project, level, window, environment and issue status.</param>
/// <param name="ProjectEvents">Event counts grouped by project, level and window, zero filled for every project.</param>
/// <param name="AlertDeliveries">Cumulative alert deliveries grouped by channel type and status.</param>
/// <param name="NewIssues">Issues first seen inside a window, grouped by project, level and window.</param>
/// <param name="Releases">Newest release observed for each project.</param>
/// <param name="DiscardedEnvelopes">Discarded envelopes still held in the database, grouped by envelope item type.</param>
/// <param name="DiscardedEnvelopeTotals">Envelopes discarded since process start, grouped by envelope item type.</param>
/// <param name="EventsIngested">Events accepted into the ingestion pipeline since process start.</param>
/// <param name="EventsDropped">Events discarded by the digestion pipeline since process start.</param>
/// <param name="DigestionBacklog">Envelopes currently awaiting digestion.</param>
/// <param name="DigestionOldestPendingSeconds">Age of the oldest envelope awaiting digestion, in seconds.</param>
/// <param name="LastIngestionUtc">Most recent accepted ingestion instant, or null when none has been accepted.</param>
/// <param name="LastPersistedIngestionUtc">Most recent accepted ingestion instant found in persisted data.</param>
/// <param name="BuildVersion">Version of the running Sigil build.</param>
public sealed record MetricsSnapshot(
    IReadOnlyList<IssueCountSample> Issues,
    IReadOnlyList<EventCountSample> Events,
    IReadOnlyList<ProjectEventCountSample> ProjectEvents,
    IReadOnlyList<AlertDeliverySample> AlertDeliveries,
    IReadOnlyList<NewIssueCountSample> NewIssues,
    IReadOnlyList<ReleaseInfoSample> Releases,
    IReadOnlyList<EnvelopeDiscardSample> DiscardedEnvelopes,
    IReadOnlyList<EnvelopeDiscardSample> DiscardedEnvelopeTotals,
    long EventsIngested,
    long EventsDropped,
    long DigestionBacklog,
    double DigestionOldestPendingSeconds,
    DateTime? LastIngestionUtc,
    DateTime? LastPersistedIngestionUtc,
    string BuildVersion);

/// <summary>Defaults for the metrics endpoint.</summary>
public static class MetricsDefaults
{
    /// <summary>Shortest event window reported by sigil_events.</summary>
    public static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);

    /// <summary>Second event window reported by sigil_events.</summary>
    public static readonly TimeSpan FifteenMinutes = TimeSpan.FromMinutes(15);

    /// <summary>Third event window reported by sigil_events.</summary>
    public static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

    /// <summary>Longest event window reported by sigil_events.</summary>
    public static readonly TimeSpan OneDay = TimeSpan.FromHours(24);

    /// <summary>Windows that sigil_events covers, shortest first.</summary>
    public static readonly IReadOnlyList<TimeSpan> EventWindows = [FiveMinutes, FifteenMinutes, OneHour, OneDay];

    /// <summary>Number of environment values reported per project before the tail is folded into one label.</summary>
    public const int EnvironmentLabelLimit = 10;

    /// <summary>Environment label used for the values beyond the per-project limit.</summary>
    public const string EnvironmentOtherLabel = "other";

    /// <summary>Environment label used for events without an environment tag.</summary>
    public const string EnvironmentMissingLabel = "none";
}
