using Sigil.Application.Models.Metrics;
using Sigil.Application.Services;
using Sigil.Domain.Enums;

namespace Sigil.Application.Tests.Services;

public class PrometheusExpositionWriterTests
{
    private static readonly DateTime IngestedAt = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static MetricsSnapshot Snapshot(
        IReadOnlyList<IssueCountSample>? issues = null,
        IReadOnlyList<EventCountSample>? events = null,
        IReadOnlyList<AlertDeliverySample>? deliveries = null,
        long ingested = 0,
        long dropped = 0,
        long backlog = 0,
        DateTime? lastIngestion = null,
        string version = "9.1.0") =>
        new(issues ?? [], events ?? [], deliveries ?? [], ingested, dropped, backlog, lastIngestion, version,
            MetricsDefaults.EventWindow);

    private static string[] Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    // ── Content type ──────────────────────────────────────────────────────────

    [Fact]
    public void ContentType_IsThePrometheusTextExpositionType()
    {
        PrometheusExpositionWriter.ContentType.Should().Be("text/plain; version=0.0.4; charset=utf-8");
    }

    // ── HELP and TYPE ────────────────────────────────────────────────────────

    [Fact]
    public void Write_DeclaresHelpAndTypeForEveryFamily()
    {
        string[] lines = Lines(PrometheusExpositionWriter.Write(Snapshot()));

        lines.Should().Contain("# HELP sigil_issues Current issue count by project, status and level.");
        lines.Should().Contain("# TYPE sigil_issues gauge");
        lines.Should().Contain("# TYPE sigil_events_total counter");
        lines.Should().Contain("# TYPE sigil_events_ingested_total counter");
        lines.Should().Contain("# TYPE sigil_events_dropped_total counter");
        lines.Should().Contain("# TYPE sigil_digestion_backlog gauge");
        lines.Should().Contain("# TYPE sigil_alert_deliveries_total counter");
        lines.Should().Contain("# TYPE sigil_last_ingestion_timestamp_seconds gauge");
        lines.Should().Contain("# TYPE sigil_build_info gauge");
    }

    [Fact]
    public void Write_DeclaresEachFamilyExactlyOnce()
    {
        string[] lines = Lines(PrometheusExpositionWriter.Write(Snapshot()));

        lines.Count(l => l.StartsWith("# TYPE ")).Should().Be(8);
        lines.Count(l => l.StartsWith("# HELP ")).Should().Be(8);
    }

    [Fact]
    public void Write_EmitsTypeBeforeAnySampleOfThatFamily()
    {
        string[] lines = Lines(PrometheusExpositionWriter.Write(Snapshot(issues:
        [
            new IssueCountSample("web", IssueStatus.Open, Severity.Error, 3)
        ])));

        int typeLine = Array.FindIndex(lines, l => l == "# TYPE sigil_issues gauge");
        int sampleLine = Array.FindIndex(lines, l => l.StartsWith("sigil_issues{"));

        typeLine.Should().BeGreaterThanOrEqualTo(0);
        sampleLine.Should().BeGreaterThan(typeLine);
    }

    [Fact]
    public void Write_HelpLinePrecedesTheTypeLine()
    {
        string[] lines = Lines(PrometheusExpositionWriter.Write(Snapshot()));

        int help = Array.FindIndex(lines, l => l.StartsWith("# HELP sigil_build_info"));
        int type = Array.FindIndex(lines, l => l.StartsWith("# TYPE sigil_build_info"));

        help.Should().BeGreaterThanOrEqualTo(0);
        type.Should().Be(help + 1);
    }

    [Fact]
    public void Write_BuildInfoIsAlwaysOne()
    {
        PrometheusExpositionWriter.Write(Snapshot(version: "1.2.3"))
            .Should().Contain("sigil_build_info{version=\"1.2.3\"} 1");
    }

    // ── Samples and label rendering ───────────────────────────────────────────

    [Fact]
    public void Write_RendersIssueSamplesWithBoundedLabels()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(issues:
        [
            new IssueCountSample("web", IssueStatus.Open, Severity.Error, 3),
            new IssueCountSample("web", IssueStatus.ResolvedInFuture, Severity.Warning, 1)
        ]));

        text.Should().Contain("sigil_issues{project=\"web\",status=\"open\",level=\"error\"} 3");
        text.Should().Contain("sigil_issues{project=\"web\",status=\"resolved_in_future\",level=\"warning\"} 1");
    }

    [Fact]
    public void Write_RendersEverySeverityLevelInLowercase()
    {
        Severity[] levels = [Severity.Debug, Severity.Info, Severity.Warning, Severity.Error, Severity.Fatal];

        string text = PrometheusExpositionWriter.Write(Snapshot(events:
            levels.Select(l => new EventCountSample("web", l, 1)).ToList()));

        foreach (string expected in new[] { "debug", "info", "warning", "error", "fatal" })
            text.Should().Contain($"level=\"{expected}\"");
    }

    [Fact]
    public void Write_RendersAlertDeliveriesByChannelAndStatus()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(deliveries:
        [
            new AlertDeliverySample(AlertChannelType.Slack, AlertDeliveryStatus.Sent, 5),
            new AlertDeliverySample(AlertChannelType.Webhook, AlertDeliveryStatus.Failed, 2)
        ]));

        text.Should().Contain("sigil_alert_deliveries_total{channel=\"slack\",status=\"sent\"} 5");
        text.Should().Contain("sigil_alert_deliveries_total{channel=\"webhook\",status=\"failed\"} 2");
    }

    [Fact]
    public void Write_RendersUnlabelledSamplesWithoutBraces()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(ingested: 12, dropped: 3, backlog: 7));

        text.Should().Contain("sigil_events_ingested_total 12");
        text.Should().Contain("sigil_events_dropped_total 3");
        text.Should().Contain("sigil_digestion_backlog 7");
    }

    [Fact]
    public void Write_EndsWithASingleTrailingNewline()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot());

        text.Should().EndWith("\n");
        text.Should().NotEndWith("\n\n");
    }

    // ── Timestamps ────────────────────────────────────────────────────────────

    [Fact]
    public void Write_RendersLastIngestionAsUnixSeconds()
    {
        PrometheusExpositionWriter.Write(Snapshot(lastIngestion: IngestedAt))
            .Should().Contain("sigil_last_ingestion_timestamp_seconds 1790856000");
    }

    [Fact]
    public void Write_RendersZeroWhenNothingHasBeenIngested()
    {
        PrometheusExpositionWriter.Write(Snapshot(lastIngestion: null))
            .Should().Contain("sigil_last_ingestion_timestamp_seconds 0");
    }

    [Fact]
    public void Write_ConvertsNonUtcLastIngestionToTheSameInstant()
    {
        var local = IngestedAt.ToLocalTime();

        local.Kind.Should().Be(DateTimeKind.Local);

        string utcText = PrometheusExpositionWriter.Write(Snapshot(lastIngestion: IngestedAt));
        string localText = PrometheusExpositionWriter.Write(Snapshot(lastIngestion: local));

        localText.Should().Be(utcText);
    }

    // ── Escaping ──────────────────────────────────────────────────────────────

    [Fact]
    public void EscapeLabelValue_EscapesBackslashQuoteAndNewline()
    {
        PrometheusExpositionWriter.EscapeLabelValue("a\\b\"c\nd")
            .Should().Be("a\\\\b\\\"c\\nd");
    }

    [Fact]
    public void EscapeLabelValue_LeavesCarriageReturnAndTabsAlone()
    {
        PrometheusExpositionWriter.EscapeLabelValue("a\rb\tc").Should().Be("a\rb\tc");
    }

    [Fact]
    public void EscapeHelp_EscapesBackslashAndNewlineButNotQuotes()
    {
        PrometheusExpositionWriter.EscapeHelp("a\\b\"c\nd")
            .Should().Be("a\\\\b\"c\\nd");
    }

    [Fact]
    public void Write_EscapesHostileProjectNamesInLabels()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(issues:
            [new IssueCountSample("we\"ird\\name\nhere", IssueStatus.Open, Severity.Info, 1)]));

        text.Should().Contain("sigil_issues{project=\"we\\\"ird\\\\name\\nhere\",status=\"open\",level=\"info\"} 1");
    }

    [Fact]
    public void Write_EverySampleLineStaysOnOneLine()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(issues:
            [new IssueCountSample("line1\nline2", IssueStatus.Open, Severity.Error, 2)]));

        text.Split('\n').Where(l => l.StartsWith("sigil_issues{")).Should().ContainSingle();
    }

    // ── Culture independence ──────────────────────────────────────────────────

    [Fact]
    public void Write_UsesInvariantFormattingUnderACommaDecimalCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            string text = PrometheusExpositionWriter.Write(Snapshot(
                ingested: 1234567,
                backlog: 89,
                lastIngestion: IngestedAt));

            text.Should().Contain("sigil_events_ingested_total 1234567");
            text.Should().Contain("sigil_digestion_backlog 89");
            text.Should().Contain("sigil_last_ingestion_timestamp_seconds 1790856000");
            text.Should().NotContain("1790856000,0");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Write_DescribesTheEventWindowInHelp()
    {
        PrometheusExpositionWriter.Write(Snapshot())
            .Should().Contain("# HELP sigil_events_total Events received in the last 24 hours, by project and level.");
    }

    // ── Cardinality bounds ────────────────────────────────────────────────────

    [Fact]
    public void Write_NeverEmitsAnIssueIdLabel()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(issues:
            [new IssueCountSample("web", IssueStatus.Open, Severity.Error, 1)]));

        text.Should().NotContain("issue_id");
        text.Should().NotContain("issue=");
    }

    [Fact]
    public void Write_NeverEmitsATagValueLabel()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(issues:
            [new IssueCountSample("web", IssueStatus.Open, Severity.Error, 1)]));

        foreach (string forbidden in new[] { "tag", "tag_key", "tag_value", "fingerprint", "title", "exception_type", "user_id", "message" })
            text.Should().NotContain(forbidden + "=\"");
    }

    [Fact]
    public void Write_EmitsOnlyTheDeclaredLabelNames()
    {
        var allowed = new HashSet<string> { "project", "status", "level", "channel", "version" };

        string text = PrometheusExpositionWriter.Write(Snapshot(
            issues: [new IssueCountSample("web", IssueStatus.Open, Severity.Error, 3)],
            events: [new EventCountSample("web", Severity.Fatal, 9)],
            deliveries: [new AlertDeliverySample(AlertChannelType.Slack, AlertDeliveryStatus.Sent, 1)],
            ingested: 5, dropped: 2, backlog: 4, lastIngestion: IngestedAt, version: "1.0.0"));

        List<string> labelNames = System.Text.RegularExpressions.Regex
            .Matches(text, @"(\w+)=""")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        labelNames.Should().OnlyContain(n => allowed.Contains(n));
    }

    [Fact]
    public void Write_IssueSeriesCountIsBoundedByProjectsTimesStatusesTimesLevels()
    {
        // 3 projects x 4 statuses x 5 levels is the theoretical maximum for sigil_issues
        var issues = new List<IssueCountSample>();
        foreach (string project in new[] { "a", "b", "c" })
            foreach (IssueStatus status in Enum.GetValues<IssueStatus>())
                foreach (Severity level in Enum.GetValues<Severity>())
                    issues.Add(new IssueCountSample(project, status, level, 1));

        string[] lines = Lines(PrometheusExpositionWriter.Write(Snapshot(issues: issues)));
        lines.Count(l => l.StartsWith("sigil_issues{")).Should().Be(3 * 4 * 5);
    }
}
