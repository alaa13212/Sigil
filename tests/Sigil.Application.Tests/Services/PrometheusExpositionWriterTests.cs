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
        IReadOnlyList<NewIssueCountSample>? newIssues = null,
        IReadOnlyList<ReleaseInfoSample>? releases = null,
        IReadOnlyList<EnvelopeDiscardSample>? discarded = null,
        long ingested = 0,
        long dropped = 0,
        long backlog = 0,
        double oldestPending = 0,
        DateTime? lastIngestion = null,
        DateTime? persistedIngestion = null,
        string version = "9.1.0") =>
        new(
            issues ?? [],
            events ?? [],
            deliveries ?? [],
            newIssues ?? [],
            releases ?? [],
            discarded ?? [],
            ingested,
            dropped,
            backlog,
            oldestPending,
            lastIngestion,
            persistedIngestion,
            version);

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
        lines.Should().Contain("# TYPE sigil_events gauge");
        lines.Should().Contain("# TYPE sigil_events_ingested_total counter");
        lines.Should().Contain("# TYPE sigil_events_dropped_total counter");
        lines.Should().Contain("# TYPE sigil_digestion_backlog gauge");
        lines.Should().Contain("# TYPE sigil_digestion_oldest_pending_seconds gauge");
        lines.Should().Contain("# TYPE sigil_envelopes_discarded_total counter");
        lines.Should().Contain("# TYPE sigil_alert_deliveries_total counter");
        lines.Should().Contain("# TYPE sigil_new_issues gauge");
        lines.Should().Contain("# TYPE sigil_release_info gauge");
        lines.Should().Contain("# TYPE sigil_release_first_seen_timestamp_seconds gauge");
        lines.Should().Contain("# TYPE sigil_last_ingestion_timestamp_seconds gauge");
        lines.Should().Contain("# TYPE sigil_build_info gauge");
    }

    [Fact]
    public void Write_DeclaresEachFamilyExactlyOnce()
    {
        string[] lines = Lines(PrometheusExpositionWriter.Write(Snapshot()));

        lines.Count(l => l.StartsWith("# TYPE ")).Should().Be(13);
        lines.Count(l => l.StartsWith("# HELP ")).Should().Be(13);
    }

    [Fact]
    public void Write_EmitsTypeBeforeAnySampleOfThatFamily()
    {
        string[] lines = Lines(PrometheusExpositionWriter.Write(Snapshot(issues:
        [
            new IssueCountSample("web", 3, IssueStatus.Open, Severity.Error, 3)
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
            new IssueCountSample("web", 3, IssueStatus.Open, Severity.Error, 3),
            new IssueCountSample("web", 3, IssueStatus.ResolvedInFuture, Severity.Warning, 1)
        ]));

        text.Should().Contain("sigil_issues{project=\"web\",project_id=\"3\",status=\"open\",level=\"error\"} 3");
        text.Should().Contain("sigil_issues{project=\"web\",project_id=\"3\",status=\"resolved_in_future\",level=\"warning\"} 1");
    }

    [Fact]
    public void Write_RendersEverySeverityLevelInLowercase()
    {
        Severity[] levels = [Severity.Debug, Severity.Info, Severity.Warning, Severity.Error, Severity.Fatal];

        string text = PrometheusExpositionWriter.Write(Snapshot(events:
            levels.Select(l => new EventCountSample("web", 3, l, MetricsDefaults.FiveMinutes, "production", IssueStatus.Open, 1)).ToList()));

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
    public void Write_RendersThePersistedIngestionWhenTheCounterIsEmpty()
    {
        // After a restart the in-process counter is empty, the persisted instant keeps the
        // staleness rule from reading the metric as "nothing for 56 years".
        string text = PrometheusExpositionWriter.Write(Snapshot(lastIngestion: null, persistedIngestion: IngestedAt));

        text.Should().Contain("sigil_last_ingestion_timestamp_seconds 1790856000");
    }

    [Fact]
    public void Write_RendersTheLaterOfTheCounterAndThePersistedIngestion()
    {
        var older = IngestedAt.AddHours(-1);

        string text = PrometheusExpositionWriter.Write(Snapshot(lastIngestion: older, persistedIngestion: IngestedAt));

        text.Should().Contain("sigil_last_ingestion_timestamp_seconds 1790856000");
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
            [new IssueCountSample("we\"ird\\name\nhere", 1, IssueStatus.Open, Severity.Info, 1)]));

        text.Should().Contain("sigil_issues{project=\"we\\\"ird\\\\name\\nhere\",project_id=\"1\",status=\"open\",level=\"info\"} 1");
    }

    [Fact]
    public void Write_EverySampleLineStaysOnOneLine()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(issues:
            [new IssueCountSample("line1\nline2", 1, IssueStatus.Open, Severity.Error, 2)]));

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
                oldestPending: 3.5,
                lastIngestion: IngestedAt));

            text.Should().Contain("sigil_events_ingested_total 1234567");
            text.Should().Contain("sigil_digestion_backlog 89");
            text.Should().Contain("sigil_last_ingestion_timestamp_seconds 1790856000");
            text.Should().NotContain("1790856000,0");
            text.Should().Contain("sigil_digestion_oldest_pending_seconds 3.5");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    // ── Event family ──────────────────────────────────────────────────────────

    [Fact]
    public void Write_DescribesTheEventWindowInHelp()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot());

        text.Should().Contain("# HELP sigil_events Events received within the window, by project, level, environment and issue status.");
    }

    [Fact]
    public void Write_DocumentsTheEnvironmentCapInTheEventHelp()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot());

        text.Should().Contain($"the {MetricsDefaults.EnvironmentLabelLimit} most frequent values per project");
        text.Should().Contain($"the remaining values are reported as {MetricsDefaults.EnvironmentOtherLabel}");
    }

    [Fact]
    public void Write_ExplainsThatOnlyObservedEventCombinationsAreReported()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot());

        text.Should().Contain("One series per observed combination, combinations without events are not reported.");
    }

    [Fact]
    public void Write_NoLongerExposesTheWindowedCountAsACounter()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(events:
            [new EventCountSample("web", 3, Severity.Error, MetricsDefaults.OneDay, "production", IssueStatus.Open, 4)]));

        // The old family was a 24 hour window typed as a counter, so rate() and increase()
        // over it produced numbers that were not event rates.
        text.Should().NotContain("sigil_events_total");
        text.Should().Contain("# TYPE sigil_events gauge");
    }

    [Fact]
    public void Write_RendersTheEventLabelsIncludingEnvironmentAndStatus()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(events:
        [
            new EventCountSample("web", 3, Severity.Error, MetricsDefaults.OneHour, "production", IssueStatus.Ignored, 7)
        ]));

        text.Should().Contain("sigil_events{project=\"web\",project_id=\"3\",level=\"error\",window=\"1h\",environment=\"production\",status=\"ignored\"} 7");
    }

    [Fact]
    public void Write_RendersTheFoldedEnvironmentLabel()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(events:
        [
            new EventCountSample("web", 3, Severity.Error, MetricsDefaults.OneHour, MetricsDefaults.EnvironmentOtherLabel, IssueStatus.Open, 2)
        ]));

        text.Should().Contain("environment=\"other\"");
    }

    // ── Cardinality bounds ────────────────────────────────────────────────────

    [Fact]
    public void Write_NeverEmitsAnIssueIdLabel()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(issues:
            [new IssueCountSample("web", 3, IssueStatus.Open, Severity.Error, 1)]));

        text.Should().NotContain("issue_id");
        text.Should().NotContain("issue=");
    }

    [Fact]
    public void Write_NeverEmitsATagValueLabel()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(issues:
            [new IssueCountSample("web", 3, IssueStatus.Open, Severity.Error, 1)]));

        foreach (string forbidden in new[] { "tag", "tag_key", "tag_value", "fingerprint", "title", "exception_type", "user_id", "message" })
            text.Should().NotContain(forbidden + "=\"");
    }

    [Fact]
    public void Write_EmitsOnlyTheDeclaredLabelNames()
    {
        var allowed = new HashSet<string>
        {
            "project", "project_id", "status", "level", "window", "environment",
            "channel", "version", "type", "release", "commit"
        };

        string text = PrometheusExpositionWriter.Write(Snapshot(
            issues: [new IssueCountSample("web", 3, IssueStatus.Open, Severity.Error, 3)],
            events: [new EventCountSample("web", 3, Severity.Fatal, MetricsDefaults.OneHour, "production", IssueStatus.Open, 9)],
            deliveries: [new AlertDeliverySample(AlertChannelType.Slack, AlertDeliveryStatus.Sent, 1)],
            newIssues: [new NewIssueCountSample("web", 3, Severity.Error, MetricsDefaults.OneHour, 2)],
            releases: [new ReleaseInfoSample("web", 3, "1.0.0", "abcdef1234", IngestedAt)],
            discarded: [new EnvelopeDiscardSample("client_report", 4)],
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
                    issues.Add(new IssueCountSample(project, 1, status, level, 1));

        string[] lines = Lines(PrometheusExpositionWriter.Write(Snapshot(issues: issues)));
        lines.Count(l => l.StartsWith("sigil_issues{")).Should().Be(3 * 4 * 5);
    }

    [Fact]
    public void Write_EventSeriesCountIsBoundedByProjectsTimesLevelsTimesWindows()
    {
        // 3 projects x 5 levels x 4 windows is the theoretical maximum for sigil_events
        var events = new List<EventCountSample>();
        foreach (string project in new[] { "a", "b", "c" })
            foreach (Severity level in Enum.GetValues<Severity>())
                foreach (TimeSpan window in MetricsDefaults.EventWindows)
                    events.Add(new EventCountSample(project, 1, level, window, "production", IssueStatus.Open, 1));

        string[] lines = Lines(PrometheusExpositionWriter.Write(Snapshot(events: events)));
        lines.Count(l => l.StartsWith("sigil_events{")).Should().Be(3 * 5 * 4);
    }

    // ── Windows ───────────────────────────────────────────────────────────────

    [Fact]
    public void Write_RendersEachWindowAsItsOwnLabelValue()
    {
        var events = new List<EventCountSample>();
        foreach (TimeSpan window in MetricsDefaults.EventWindows)
            events.Add(new EventCountSample("web", 3, Severity.Error, window, "production", IssueStatus.Open, 1));

        string text = PrometheusExpositionWriter.Write(Snapshot(events: events));

        text.Should().Contain("window=\"5m\"");
        text.Should().Contain("window=\"15m\"");
        text.Should().Contain("window=\"1h\"");
        text.Should().Contain("window=\"24h\"");
    }

    [Fact]
    public void Write_RendersTheSameLevelInDifferentWindowsAsDifferentSeries()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(events:
        [
            new EventCountSample("web", 3, Severity.Error, MetricsDefaults.FiveMinutes, "production", IssueStatus.Open, 2),
            new EventCountSample("web", 3, Severity.Error, MetricsDefaults.OneHour, "production", IssueStatus.Open, 40),
        ]));

        text.Should().Contain("sigil_events{project=\"web\",project_id=\"3\",level=\"error\",window=\"5m\",environment=\"production\",status=\"open\"} 2");
        text.Should().Contain("sigil_events{project=\"web\",project_id=\"3\",level=\"error\",window=\"1h\",environment=\"production\",status=\"open\"} 40");
    }

    // ── Severity separation ───────────────────────────────────────────────────

    [Fact]
    public void Write_KeepsErrorAndWarningCountsInSeparateSeries()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(events:
        [
            new EventCountSample("web", 3, Severity.Error, MetricsDefaults.FiveMinutes, "production", IssueStatus.Open, 7),
            new EventCountSample("web", 3, Severity.Warning, MetricsDefaults.FiveMinutes, "production", IssueStatus.Open, 300),
        ]));

        text.Should().Contain("level=\"error\"");
        text.Should().Contain("level=\"warning\"");
    }

    [Fact]
    public void Write_KeepsFatalInItsOwnSeriesSoErrorQueriesCanExcludeIt()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(events:
        [
            new EventCountSample("web", 3, Severity.Fatal, MetricsDefaults.FiveMinutes, "production", IssueStatus.Open, 1),
            new EventCountSample("web", 3, Severity.Error, MetricsDefaults.FiveMinutes, "production", IssueStatus.Open, 2),
        ]));

        text.Should().Contain("level=\"fatal\"");
        text.Should().Contain("level=\"error\"");
    }

    [Fact]
    public void Write_NeverMergesTwoProjectsIntoOneSeries()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(events:
        [
            new EventCountSample("web", 3, Severity.Error, MetricsDefaults.FiveMinutes, "production", IssueStatus.Open, 1),
            new EventCountSample("api", 4, Severity.Error, MetricsDefaults.FiveMinutes, "production", IssueStatus.Open, 1),
        ]));

        text.Should().Contain("project=\"web\",project_id=\"3\"");
        text.Should().Contain("project=\"api\",project_id=\"4\"");
    }

    // ── Project identifier ────────────────────────────────────────────────────

    [Fact]
    public void Write_PutsTheProjectIdentifierNextToTheProjectOnEveryProjectFamily()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(
            issues: [new IssueCountSample("web", 12, IssueStatus.Open, Severity.Error, 3)],
            events: [new EventCountSample("web", 12, Severity.Error, MetricsDefaults.OneHour, "production", IssueStatus.Open, 9)],
            newIssues: [new NewIssueCountSample("web", 12, Severity.Error, MetricsDefaults.OneHour, 7)],
            releases: [new ReleaseInfoSample("web", 12, "1.0.0+3f2a9c1", "3f2a9c1", IngestedAt)]));

        text.Should().Contain("sigil_issues{project=\"web\",project_id=\"12\"");
        text.Should().Contain("sigil_events{project=\"web\",project_id=\"12\"");
        text.Should().Contain("sigil_new_issues{project=\"web\",project_id=\"12\"");
        text.Should().Contain("sigil_release_info{project=\"web\",project_id=\"12\"");
        text.Should().Contain("sigil_release_first_seen_timestamp_seconds{project=\"web\",project_id=\"12\"}");
    }

    // ── Digestion gauges ──────────────────────────────────────────────────────

    [Fact]
    public void Write_RendersTheOldestPendingAge()
    {
        PrometheusExpositionWriter.Write(Snapshot(oldestPending: 3.2))
            .Should().Contain("sigil_digestion_oldest_pending_seconds 3.2");
    }

    [Fact]
    public void Write_RendersZeroOldestPendingAgeWhenTheQueueIsEmpty()
    {
        PrometheusExpositionWriter.Write(Snapshot(backlog: 0, oldestPending: 0))
            .Should().Contain("sigil_digestion_oldest_pending_seconds 0");
    }

    [Fact]
    public void Write_RendersDiscardedEnvelopesByItemType()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(discarded:
        [
            new EnvelopeDiscardSample("client_report", 12),
            new EnvelopeDiscardSample("transaction", 4),
        ]));

        text.Should().Contain("sigil_envelopes_discarded_total{type=\"client_report\"} 12");
        text.Should().Contain("sigil_envelopes_discarded_total{type=\"transaction\"} 4");
    }

    // ── New issues ────────────────────────────────────────────────────────────

    [Fact]
    public void Write_RendersNewIssuesWithLevelAndWindow()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(newIssues:
        [
            new NewIssueCountSample("web", 12, Severity.Error, MetricsDefaults.OneHour, 7)
        ]));

        text.Should().Contain("sigil_new_issues{project=\"web\",project_id=\"12\",level=\"error\",window=\"1h\"} 7");
    }

    // ── Release info ──────────────────────────────────────────────────────────

    [Fact]
    public void Write_RendersTheNewestReleasePerProject()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(releases:
        [
            new ReleaseInfoSample("Kammelna ASP", 12, "1.0.0+3f2a9c1", "3f2a9c1", IngestedAt)
        ]));

        text.Should().Contain("sigil_release_info{project=\"Kammelna ASP\",project_id=\"12\",release=\"1.0.0+3f2a9c1\",commit=\"3f2a9c1\"} 1");
    }

    [Fact]
    public void Write_TruncatesTheCommitShaToSevenCharacters()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(releases:
        [
            new ReleaseInfoSample("web", 12, "1.0.0", "3f2a9c1e9b8d4a6c", IngestedAt)
        ]));

        text.Should().Contain("commit=\"3f2a9c1\"");
    }

    [Fact]
    public void Write_RendersAnEmptyCommitWhenTheReleaseCarriesNone()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(releases:
        [
            new ReleaseInfoSample("web", 12, "1.0.0", null, IngestedAt)
        ]));

        text.Should().Contain("sigil_release_info{project=\"web\",project_id=\"12\",release=\"1.0.0\",commit=\"\"} 1");
    }

    [Fact]
    public void Write_RendersTheReleaseFirstSeenAsUnixSeconds()
    {
        string text = PrometheusExpositionWriter.Write(Snapshot(releases:
        [
            new ReleaseInfoSample("web", 12, "1.0.0", null, IngestedAt)
        ]));

        text.Should().Contain("sigil_release_first_seen_timestamp_seconds{project=\"web\",project_id=\"12\"} 1790856000");
    }
}
