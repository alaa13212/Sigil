using System.Globalization;
using System.Text;
using Sigil.Application.Models.Metrics;
using Sigil.Domain.Enums;

namespace Sigil.Application.Services;

/// <summary>Renders a <see cref="MetricsSnapshot"/> as Prometheus text exposition format.</summary>
public static class PrometheusExpositionWriter
{
    /// <summary>Content type declared by the Prometheus text exposition format.</summary>
    public const string ContentType = "text/plain; version=0.0.4; charset=utf-8";

    /// <summary>Number of characters kept from a release commit sha.</summary>
    public const int CommitShaLength = 7;

    /// <summary>Renders the snapshot as exposition text.</summary>
    public static string Write(MetricsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sb = new StringBuilder();

        Family(sb, "sigil_issues", "gauge",
            "Current issue count by project, status and level.",
            snapshot.Issues.Select(i =>
                Sample(
                    new[]
                    {
                        Label("project", i.Project),
                        Label("project_id", Number(i.ProjectId)),
                        Label("status", StatusName(i.Status)),
                        Label("level", SeverityName(i.Level)),
                    },
                    i.Count)));

        Family(sb, "sigil_events", "gauge",
            EventsHelp(),
            snapshot.Events.Select(e =>
                Sample(
                    new[]
                    {
                        Label("project", e.Project),
                        Label("project_id", Number(e.ProjectId)),
                        Label("level", SeverityName(e.Level)),
                        Label("window", WindowName(e.Window)),
                        Label("environment", e.Environment),
                        Label("status", StatusName(e.Status)),
                    },
                    e.Count)));

        Family(sb, "sigil_events_ingested_total", "counter",
            "Events accepted into the ingestion pipeline since process start.",
            [Sample([], snapshot.EventsIngested)]);

        Family(sb, "sigil_events_dropped_total", "counter",
            "Events discarded by the digestion pipeline since process start.",
            [Sample([], snapshot.EventsDropped)]);

        Family(sb, "sigil_digestion_backlog", "gauge",
            "Envelopes currently awaiting digestion.",
            [Sample([], snapshot.DigestionBacklog)]);

        Family(sb, "sigil_digestion_oldest_pending_seconds", "gauge",
            "Age of the oldest envelope still awaiting digestion, in seconds. Zero when the queue is empty.",
            [Sample([], snapshot.DigestionOldestPendingSeconds)]);

        Family(sb, "sigil_envelopes_discarded_total", "counter",
            "Envelopes discarded because Sigil does not process their item type, by envelope item type. " +
            "Counts the discarded envelopes still held in the database; retention removes older rows.",
            snapshot.DiscardedEnvelopes.Select(d =>
                Sample(new[] { Label("type", d.Type) }, d.Count)));

        Family(sb, "sigil_alert_deliveries_total", "counter",
            "Alert deliveries recorded, by channel type and delivery status.",
            snapshot.AlertDeliveries.Select(a =>
                Sample(
                    new[] { Label("channel", a.Channel.ToString().ToLowerInvariant()), Label("status", a.Status.ToString().ToLowerInvariant()) },
                    a.Count)));

        Family(sb, "sigil_new_issues", "gauge",
            "Issues first seen inside the window, by project and level. One series per observed combination.",
            snapshot.NewIssues.Select(n =>
                Sample(
                    new[]
                    {
                        Label("project", n.Project),
                        Label("project_id", Number(n.ProjectId)),
                        Label("level", SeverityName(n.Level)),
                        Label("window", WindowName(n.Window)),
                    },
                    n.Count)));

        Family(sb, "sigil_release_info", "gauge",
            "Newest release seen for each project. Always 1.",
            snapshot.Releases.Select(r =>
                Sample(
                    new[]
                    {
                        Label("project", r.Project),
                        Label("project_id", Number(r.ProjectId)),
                        Label("release", r.Release),
                        Label("commit", CommitSha(r.Commit)),
                    },
                    1L)));

        Family(sb, "sigil_release_first_seen_timestamp_seconds", "gauge",
            "Unix timestamp of the first time the newest release of a project was seen.",
            snapshot.Releases.Select(r =>
                Sample(
                    new[]
                    {
                        Label("project", r.Project),
                        Label("project_id", Number(r.ProjectId)),
                    },
                    UnixSeconds(r.FirstSeenAt))));

        Family(sb, "sigil_last_ingestion_timestamp_seconds", "gauge",
            "Unix timestamp of the most recently ingested event, or 0 when none has been ingested. " +
            "Seeded from persisted data, so it survives a restart.",
            [Sample([], UnixSeconds(LatestIngestion(snapshot)))]);

        Family(sb, "sigil_build_info", "gauge",
            "Build information for the running Sigil instance. Always 1.",
            [Sample([Label("version", snapshot.BuildVersion)], 1L)]);

        return sb.ToString();
    }

    /// <summary>Escapes a label value: backslash, double quote and line feed only.</summary>
    public static string EscapeLabelValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }

    /// <summary>Escapes HELP text: backslash and line feed only. Double quotes are literal.</summary>
    public static string EscapeHelp(string help)
    {
        ArgumentNullException.ThrowIfNull(help);

        var sb = new StringBuilder(help.Length);
        foreach (char c in help)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }

    /// <summary>Help text of the sigil_events family.</summary>
    private static string EventsHelp() =>
        "Events received within the window, by project, level, environment and issue status. " +
        "One series per observed combination, combinations without events are not reported. " +
        $"Environment values are capped at the {MetricsDefaults.EnvironmentLabelLimit} most frequent values per project, " +
        $"the remaining values are reported as {MetricsDefaults.EnvironmentOtherLabel} " +
        $"and events without an environment tag as {MetricsDefaults.EnvironmentMissingLabel}.";

    private static void Family(StringBuilder sb, string name, string type, string help, IEnumerable<(string Labels, string Value)> samples)
    {
        sb.Append("# HELP ").Append(name).Append(' ').Append(EscapeHelp(help)).Append('\n');
        sb.Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');

        foreach (var sample in samples)
            sb.Append(name).Append(sample.Labels).Append(' ').Append(sample.Value).Append('\n');
    }

    private static (string Labels, string Value) Sample(IReadOnlyList<(string Key, string Value)> labels, long value) =>
        Render(labels, value.ToString(CultureInfo.InvariantCulture));

    private static (string Labels, string Value) Sample(IReadOnlyList<(string Key, string Value)> labels, double value) =>
        Render(labels, value.ToString("0.###", CultureInfo.InvariantCulture));

    private static (string Labels, string Value) Sample(IReadOnlyList<(string Key, string Value)> labels, string value) =>
        Render(labels, value);

    private static (string Labels, string Value) Render(IReadOnlyList<(string Key, string Value)> labels, string value)
    {
        string rendered = string.Join(",", labels.Select(l => $"{l.Key}=\"{EscapeLabelValue(l.Value)}\""));
        return (rendered.Length == 0 ? "" : "{" + rendered + "}", value);
    }

    private static (string Key, string Value) Label(string key, string value) => (key, value);

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Short form of a commit sha, empty when the release carries no commit.</summary>
    private static string CommitSha(string? commit)
    {
        if (string.IsNullOrWhiteSpace(commit))
            return "";

        return commit.Length <= CommitShaLength ? commit : commit[..CommitShaLength];
    }

    /// <summary>Later of the in-process counter and the persisted ingestion instant.</summary>
    private static DateTime? LatestIngestion(MetricsSnapshot snapshot)
    {
        if (snapshot.LastIngestionUtc is null)
            return snapshot.LastPersistedIngestionUtc;

        if (snapshot.LastPersistedIngestionUtc is null)
            return snapshot.LastIngestionUtc;

        return snapshot.LastIngestionUtc >= snapshot.LastPersistedIngestionUtc
            ? snapshot.LastIngestionUtc
            : snapshot.LastPersistedIngestionUtc;
    }

    private static string UnixSeconds(DateTime? utc)
    {
        if (utc is null)
            return "0";

        DateTime epoch = DateTime.UnixEpoch;
        double seconds = (utc.Value.ToUniversalTime() - epoch).TotalSeconds;
        return seconds.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string WindowName(TimeSpan window)
    {
        if (window.TotalSeconds < 60)
            return window.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";

        if (window.TotalMinutes < 60)
            return window.TotalMinutes.ToString("0.###", CultureInfo.InvariantCulture) + "m";

        return window.TotalHours.ToString("0.###", CultureInfo.InvariantCulture) + "h";
    }

    private static string StatusName(IssueStatus status) => status switch
    {
        IssueStatus.ResolvedInFuture => "resolved_in_future",
        _ => status.ToString().ToLowerInvariant()
    };

    private static string SeverityName(Severity level) => level switch
    {
        Severity.Fatal => "fatal",
        Severity.Error => "error",
        Severity.Warning => "warning",
        Severity.Info => "info",
        Severity.Debug => "debug",
        _ => level.ToString().ToLowerInvariant()
    };
}
