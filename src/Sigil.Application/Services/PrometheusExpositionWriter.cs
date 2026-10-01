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

    /// <summary>Renders the snapshot as exposition text.</summary>
    public static string Write(MetricsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var sb = new StringBuilder();
        var window = FormatWindow(snapshot.EventWindow);

        Family(sb, "sigil_issues", "gauge",
            "Current issue count by project, status and level.",
            snapshot.Issues.Select(i =>
                Sample(
                    new[] { Label("project", i.Project), Label("status", StatusName(i.Status)), Label("level", SeverityName(i.Level)) },
                    i.Count)));

        Family(sb, "sigil_events_total", "counter",
            $"Events received in the last {window}, by project and level.",
            snapshot.Events.Select(e =>
                Sample(
                    new[] { Label("project", e.Project), Label("level", SeverityName(e.Level)) },
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

        Family(sb, "sigil_alert_deliveries_total", "counter",
            "Alert deliveries recorded, by channel type and delivery status.",
            snapshot.AlertDeliveries.Select(a =>
                Sample(
                    new[] { Label("channel", a.Channel.ToString().ToLowerInvariant()), Label("status", a.Status.ToString().ToLowerInvariant()) },
                    a.Count)));

        Family(sb, "sigil_last_ingestion_timestamp_seconds", "gauge",
            "Unix timestamp of the most recently ingested event, or 0 when none has been ingested.",
            [Sample([], UnixSeconds(snapshot.LastIngestionUtc))]);
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

    private static void Family(StringBuilder sb, string name, string type, string help, IEnumerable<(string Labels, string Value)> samples)
    {
        sb.Append("# HELP ").Append(name).Append(' ').Append(EscapeHelp(help)).Append('\n');
        sb.Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');

        foreach (var sample in samples)
            sb.Append(name).Append(sample.Labels).Append(' ').Append(sample.Value).Append('\n');
    }

    private static (string Labels, string Value) Sample(IReadOnlyList<(string Key, string Value)> labels, long value) =>
        Render(labels, value.ToString(CultureInfo.InvariantCulture));

    private static (string Labels, string Value) Sample(IReadOnlyList<(string Key, string Value)> labels, string value) =>
        Render(labels, value);

    private static (string Labels, string Value) Render(IReadOnlyList<(string Key, string Value)> labels, string value)
    {
        string rendered = string.Join(",", labels.Select(l => $"{l.Key}=\"{EscapeLabelValue(l.Value)}\""));
        return (rendered.Length == 0 ? "" : "{" + rendered + "}", value);
    }

    private static (string Key, string Value) Label(string key, string value) => (key, value);

    private static string UnixSeconds(DateTime? utc)
    {
        if (utc is null)
            return "0";

        DateTime epoch = DateTime.UnixEpoch;
        double seconds = (utc.Value.ToUniversalTime() - epoch).TotalSeconds;
        return seconds.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatWindow(TimeSpan window) =>
        window.TotalHours % 1 == 0
            ? ((int)window.TotalHours).ToString(CultureInfo.InvariantCulture) + " hours"
            : window.ToString("g", CultureInfo.InvariantCulture);

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
