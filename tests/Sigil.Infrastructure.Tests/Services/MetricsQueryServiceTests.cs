using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Metrics;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;
using Sigil.Infrastructure.Tests.Fixtures;
using Sigil.Infrastructure.Tests.Persistence;

namespace Sigil.Infrastructure.Tests.Services;

/// <summary>Records emits so a test can assert a worker called the abstraction rather than a concrete store.</summary>
public sealed class FakeSigilMetrics : ISigilMetrics
{
    public int IngestedCalls { get; private set; }
    public int DroppedCalls { get; private set; }
    public List<int> IngestedCounts { get; } = [];
    public List<int> DroppedCounts { get; } = [];
    public List<DateTime> LastIngestions { get; } = [];

    private SigilMetricsSnapshot _snapshot = new(0, 0, null);

    public void RecordEventsIngested(int count)
    {
        IngestedCalls++;
        IngestedCounts.Add(count);
    }

    public void RecordEventsDropped(int count)
    {
        DroppedCalls++;
        DroppedCounts.Add(count);
    }

    public void RecordLastIngestion(DateTime receivedAtUtc) => LastIngestions.Add(receivedAtUtc);

    public void SetSnapshot(SigilMetricsSnapshot snapshot) => _snapshot = snapshot;

    public SigilMetricsSnapshot GetSnapshot() => _snapshot;
}

[Collection(DbCollection)]
public class MetricsQueryServiceTests(TestDatabaseFixture fixture)
{
    private readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private class FixedDateTime(DateTime utcNow) : IDateTime
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private (SigilDbContext Db, MetricsQueryService Service, FakeSigilMetrics Metrics) CreateService()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SigilDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
        var metrics = new FakeSigilMetrics();
        return (db, new MetricsQueryService(db, new FixedDateTime(_now), metrics), metrics);
    }

    private static Project NewProject(string name) =>
        new() { Name = name, ApiKey = Guid.NewGuid().ToString("N"), Platform = Platform.CSharp };

    private async Task<Project> SeedProjectAsync(string name)
    {
        var (db, _, _) = CreateService();
        var project = NewProject(name);
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project;
    }

    [Fact]
    public async Task GetSnapshot_ReportsIssueCountsGroupedByProjectStatusAndLevel()
    {
        var project = await SeedProjectAsync("metrics-issues-project");
        var (db, service, _) = CreateService();

        db.Issues.AddRange(
            NewIssue(project.Id, IssueStatus.Open, Severity.Error),
            NewIssue(project.Id, IssueStatus.Open, Severity.Error),
            NewIssue(project.Id, IssueStatus.Resolved, Severity.Warning));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();
        var sample = snapshot.Issues.Single(i => i.Project == "metrics-issues-project" && i.Status == IssueStatus.Open && i.Level == Severity.Error);

        sample.Count.Should().Be(2);
        sample.ProjectId.Should().Be(project.Id);
        snapshot.Issues.Should().Contain(i => i.Project == "metrics-issues-project" && i.Status == IssueStatus.Resolved && i.Count == 1);
    }

    [Fact]
    public async Task GetSnapshot_CountsEventsIntoEachWindow()
    {
        var project = await SeedProjectAsync("metrics-window-project");
        var (db, service, _) = CreateService();

        var issue = NewIssue(project.Id, IssueStatus.Open, Severity.Error);
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        db.Events.AddRange(
            SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddMinutes(-1)),
            SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddMinutes(-10)),
            SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddMinutes(-30)),
            SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddHours(-5)),
            SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddHours(-48)));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        long Count(TimeSpan window) => snapshot.Events
            .Single(e => e.Project == "metrics-window-project" && e.Level == Severity.Error && e.Window == window)
            .Count;

        Count(MetricsDefaults.FiveMinutes).Should().Be(1);
        Count(MetricsDefaults.FifteenMinutes).Should().Be(2);
        Count(MetricsDefaults.OneHour).Should().Be(3);
        Count(MetricsDefaults.OneDay).Should().Be(4);
    }

    [Fact]
    public async Task GetSnapshot_CountsSeveritiesSeparately()
    {
        var project = await SeedProjectAsync("metrics-severity-project");
        var (db, service, _) = CreateService();

        var issue = NewIssue(project.Id, IssueStatus.Open, Severity.Error);
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        db.Events.AddRange(
            SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddMinutes(-1)),
            SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddMinutes(-1)),
            SeedEventIn(db, project.Id, issue.Id, Severity.Warning, _now.AddMinutes(-1)),
            SeedEventIn(db, project.Id, issue.Id, Severity.Info, _now.AddMinutes(-1)));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        long Count(Severity level) => snapshot.Events
            .Single(e => e.Project == "metrics-severity-project" && e.Level == level && e.Window == MetricsDefaults.FiveMinutes)
            .Count;

        Count(Severity.Error).Should().Be(2);
        Count(Severity.Warning).Should().Be(1);
        Count(Severity.Info).Should().Be(1);
    }

    [Fact]
    public async Task GetSnapshot_ReportsOnlyObservedEventCombinations()
    {
        var project = await SeedProjectAsync("metrics-zero-project");
        var (db, service, _) = CreateService();

        var issue = NewIssue(project.Id, IssueStatus.Open, Severity.Error);
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        db.Events.Add(SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddMinutes(-1)));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        var forProject = snapshot.Events.Where(e => e.Project == "metrics-zero-project").ToList();

        // Only the observed combination is reported, the label cross-product is unbounded.
        forProject.Should().HaveCount(MetricsDefaults.EventWindows.Count);
        forProject.Select(e => e.Level).Distinct().Should().ContainSingle().Which.Should().Be(Severity.Error);
        forProject.Select(e => e.Window).Distinct().Should().BeEquivalentTo(MetricsDefaults.EventWindows);
        forProject.Should().OnlyContain(e => e.Count == 1);
    }

    [Fact]
    public async Task GetSnapshot_ReportsNoEventSeriesForAProjectWithoutAnyEvents()
    {
        await SeedProjectAsync("metrics-silent-project");
        var (_, service, _) = CreateService();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        snapshot.Events.Where(e => e.Project == "metrics-silent-project").Should().BeEmpty();
    }

    [Fact]
    public async Task GetSnapshot_ReportsTheIssueStatusAsTheEventStatus()
    {
        var project = await SeedProjectAsync("metrics-status-project");
        var (db, service, _) = CreateService();

        var open = NewIssue(project.Id, IssueStatus.Open, Severity.Error);
        var ignored = NewIssue(project.Id, IssueStatus.Ignored, Severity.Error);
        db.Issues.AddRange(open, ignored);
        await db.SaveChangesAsync();

        db.Events.AddRange(
            SeedEventIn(db, project.Id, open.Id, Severity.Error, _now.AddMinutes(-1)),
            SeedEventIn(db, project.Id, ignored.Id, Severity.Error, _now.AddMinutes(-1)));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        var forProject = snapshot.Events
            .Where(e => e.Project == "metrics-status-project" && e.Window == MetricsDefaults.FiveMinutes)
            .ToList();

        forProject.Should().HaveCount(2);
        forProject.Should().Contain(e => e.Status == IssueStatus.Open);
        forProject.Should().Contain(e => e.Status == IssueStatus.Ignored);
    }

    [Fact]
    public async Task GetSnapshot_ReportsTheEnvironmentOfAnEvent()
    {
        var project = await SeedProjectAsync("metrics-environment-project");
        var (db, service, _) = CreateService();

        var issue = NewIssue(project.Id, IssueStatus.Open, Severity.Error);
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        var captured = SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddMinutes(-1));
        await db.SaveChangesAsync();
        await TestHelper.TagEventAsync(db, captured.Id, "environment", "production");

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        snapshot.Events
            .Should().Contain(e => e.Project == "metrics-environment-project" && e.Environment == "production");
    }

    [Fact]
    public async Task GetSnapshot_CapsTheEnvironmentValuesOfAProject()
    {
        var project = await SeedProjectAsync("metrics-environment-cap-project");
        var (db, service, _) = CreateService();

        var issue = NewIssue(project.Id, IssueStatus.Open, Severity.Error);
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        // Twelve environments, the last two are the least frequent ones.
        for (int i = 1; i <= MetricsDefaults.EnvironmentLabelLimit + 2; i++)
        {
            int count = MetricsDefaults.EnvironmentLabelLimit + 3 - i;
            for (int c = 0; c < count; c++)
            {
                var captured = SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddMinutes(-1));
                await db.SaveChangesAsync();
                await TestHelper.TagEventAsync(db, captured.Id, "environment", $"env-{i:D2}");
            }
        }

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        var environments = snapshot.Events
            .Where(e => e.Project == "metrics-environment-cap-project")
            .Select(e => e.Environment)
            .Distinct()
            .ToList();

        environments.Should().HaveCount(MetricsDefaults.EnvironmentLabelLimit + 1);
        environments.Should().Contain(MetricsDefaults.EnvironmentOtherLabel);
        environments.Should().NotContain("env-11");
        environments.Should().NotContain("env-12");
    }

    [Fact]
    public async Task GetSnapshot_ReportsTheMissingEnvironmentLabelForEventsWithoutOne()
    {
        var project = await SeedProjectAsync("metrics-environment-missing-project");
        var (db, service, _) = CreateService();

        var issue = NewIssue(project.Id, IssueStatus.Open, Severity.Error);
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        db.Events.Add(SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddMinutes(-1)));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        snapshot.Events
            .Should().Contain(e => e.Project == "metrics-environment-missing-project" && e.Environment == MetricsDefaults.EnvironmentMissingLabel);
    }

    [Fact]
    public async Task GetSnapshot_CountsAlertDeliveriesByChannelTypeAndStatus()
    {
        var project = await SeedProjectAsync("metrics-alerts-project");
        var (db, service, _) = CreateService();

        var slack = new AlertChannel { Name = "slack", Type = AlertChannelType.Slack, Config = "{}", CreatedAt = _now };
        var webhook = new AlertChannel { Name = "hook", Type = AlertChannelType.Webhook, Config = "{}", CreatedAt = _now };
        db.AlertChannels.AddRange(slack, webhook);
        await db.SaveChangesAsync();

        var slackRule = new AlertRule { ProjectId = project.Id, Name = "slack-rule", Trigger = AlertTrigger.NewIssue, AlertChannelId = slack.Id, CreatedAt = _now };
        var webhookRule = new AlertRule { ProjectId = project.Id, Name = "hook-rule", Trigger = AlertTrigger.NewIssue, AlertChannelId = webhook.Id, CreatedAt = _now };
        db.AlertRules.AddRange(slackRule, webhookRule);
        await db.SaveChangesAsync();

        var before = (await service.GetSnapshotAsync()).AlertDeliveries
            .ToDictionary(a => (a.Channel, a.Status), a => a.Count);

        db.AlertHistory.AddRange(
            new AlertHistory { AlertRuleId = slackRule.Id, FiredAt = _now, Status = AlertDeliveryStatus.Sent },
            new AlertHistory { AlertRuleId = slackRule.Id, FiredAt = _now, Status = AlertDeliveryStatus.Sent },
            new AlertHistory { AlertRuleId = slackRule.Id, FiredAt = _now, Status = AlertDeliveryStatus.Failed },
            new AlertHistory { AlertRuleId = webhookRule.Id, FiredAt = _now, Status = AlertDeliveryStatus.Throttled });
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        long Count(AlertChannelType channel, AlertDeliveryStatus status) =>
            snapshot.AlertDeliveries.SingleOrDefault(a => a.Channel == channel && a.Status == status)?.Count ?? 0;

        long Baseline(AlertChannelType channel, AlertDeliveryStatus status) =>
            before.GetValueOrDefault((channel, status));

        (Count(AlertChannelType.Slack, AlertDeliveryStatus.Sent) - Baseline(AlertChannelType.Slack, AlertDeliveryStatus.Sent))
            .Should().Be(2);
        (Count(AlertChannelType.Slack, AlertDeliveryStatus.Failed) - Baseline(AlertChannelType.Slack, AlertDeliveryStatus.Failed))
            .Should().Be(1);
        (Count(AlertChannelType.Webhook, AlertDeliveryStatus.Throttled) - Baseline(AlertChannelType.Webhook, AlertDeliveryStatus.Throttled))
            .Should().Be(1);
    }

    [Fact]
    public async Task GetSnapshot_ReportsPendingEnvelopesAsTheBacklog()
    {
        var project = await SeedProjectAsync("metrics-backlog-project");
        var (db, service, _) = CreateService();

        long before = (await service.GetSnapshotAsync()).DigestionBacklog;

        db.RawEnvelopes.AddRange(
            NewEnvelope(project.Id, error: null),
            NewEnvelope(project.Id, error: null),
            NewEnvelope(project.Id, error: "parse failed"));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        // Envelopes with a recorded error have left the queue, so only the two pending ones count.
        (snapshot.DigestionBacklog - before).Should().Be(2);
    }

    [Fact]
    public async Task GetSnapshot_ReportsTheAgeOfTheOldestPendingEnvelope()
    {
        var project = await SeedProjectAsync("metrics-oldest-pending-project");
        var (db, service, _) = CreateService();

        db.RawEnvelopes.Add(NewEnvelope(project.Id, error: null, receivedAt: _now.AddSeconds(-100)));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        DateTime? oldest = await db.RawEnvelopes
            .Where(r => r.Error == null)
            .OrderBy(r => r.ReceivedAt)
            .Select(r => (DateTime?)r.ReceivedAt)
            .FirstOrDefaultAsync();

        oldest.Should().NotBeNull();

        double expected = (_now - oldest!.Value).TotalSeconds;
        snapshot.DigestionOldestPendingSeconds.Should().BeApproximately(expected, 0.001);
        snapshot.DigestionOldestPendingSeconds.Should().BeGreaterThanOrEqualTo(100);
    }

    [Fact]
    public async Task GetSnapshot_ExcludesDiscardedEnvelopesFromTheBacklogAndTheOldestPendingAge()
    {
        var project = await SeedProjectAsync("metrics-discarded-project");
        var (db, service, _) = CreateService();

        long backlogBefore = (await service.GetSnapshotAsync()).DigestionBacklog;

        var service2 = new RawEnvelopeService(db, new FixedDateTime(_now));
        await service2.StoreAsync(project.Id, ClientReportEnvelope(), _now.AddMinutes(-30));

        var discarded = await db.RawEnvelopes
            .Where(r => r.ProjectId == project.Id)
            .OrderByDescending(r => r.Id)
            .FirstAsync();

        discarded.Error.Should().Be("unsupported envelope type: client_report");
        discarded.ProcessedAt.Should().NotBeNull();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        // A discarded envelope has left the queue, so it is neither backlog nor pending age.
        (snapshot.DigestionBacklog - backlogBefore).Should().Be(0);
        (await db.RawEnvelopes.Where(r => r.Error == null).AnyAsync(r => r.Id == discarded.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task GetSnapshot_CountsDiscardedEnvelopesByItemType()
    {
        var project = await SeedProjectAsync("metrics-discarded-count-project");
        var (db, service, _) = CreateService();

        var service2 = new RawEnvelopeService(db, new FixedDateTime(_now));
        await service2.StoreAsync(project.Id, ClientReportEnvelope(), _now);
        await service2.StoreAsync(project.Id, ClientReportEnvelope(), _now);
        await service2.StoreAsync(project.Id, TransactionEnvelope(), _now);

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        snapshot.DiscardedEnvelopes
            .Should().Contain(d => d.Type == "client_report" && d.Count >= 2);
        snapshot.DiscardedEnvelopes
            .Should().Contain(d => d.Type == "transaction" && d.Count >= 1);
    }

    [Fact]
    public async Task GetSnapshot_CountsNewIssuesPerWindow()
    {
        var project = await SeedProjectAsync("metrics-new-issues-project");
        var (db, service, _) = CreateService();

        db.Issues.AddRange(
            NewIssue(project.Id, IssueStatus.Open, Severity.Error, firstSeen: _now.AddMinutes(-1)),
            NewIssue(project.Id, IssueStatus.Open, Severity.Error, firstSeen: _now.AddMinutes(-1)),
            NewIssue(project.Id, IssueStatus.Open, Severity.Warning, firstSeen: _now.AddMinutes(-10)),
            NewIssue(project.Id, IssueStatus.Open, Severity.Error, firstSeen: _now.AddMinutes(-30)),
            NewIssue(project.Id, IssueStatus.Open, Severity.Error, firstSeen: _now.AddHours(-5)));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        long Count(Severity level, TimeSpan window) => snapshot.NewIssues
            .Single(n => n.Project == "metrics-new-issues-project" && n.Level == level && n.Window == window)
            .Count;

        Count(Severity.Error, MetricsDefaults.FiveMinutes).Should().Be(2);
        Count(Severity.Error, MetricsDefaults.FifteenMinutes).Should().Be(2);
        Count(Severity.Error, MetricsDefaults.OneHour).Should().Be(3);
        Count(Severity.Error, MetricsDefaults.OneDay).Should().Be(4);
        Count(Severity.Warning, MetricsDefaults.OneHour).Should().Be(1);
    }

    [Fact]
    public async Task GetSnapshot_ReportsTheNewestReleaseOfEachProject()
    {
        var project = await SeedProjectAsync("metrics-release-project");
        var (db, service, _) = CreateService();

        db.Releases.AddRange(
            new Release { ProjectId = project.Id, RawName = "metrics-release-1.0.0", FirstSeenAt = _now.AddDays(-2), CommitSha = "aaaaaaaaaaaa" },
            new Release { ProjectId = project.Id, RawName = "metrics-release-1.1.0", FirstSeenAt = _now.AddDays(-1), CommitSha = "bbbbbbbbbb" },
            new Release { ProjectId = project.Id, RawName = "metrics-release-1.2.0", FirstSeenAt = _now, CommitSha = "3f2a9c1e9b8d" });
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        var sample = snapshot.Releases.Single(r => r.Project == "metrics-release-project");

        sample.Release.Should().Be("metrics-release-1.2.0");
        sample.Commit.Should().Be("3f2a9c1e9b8d");
        sample.ProjectId.Should().Be(project.Id);
        sample.FirstSeenAt.Should().Be(_now);
    }

    [Fact]
    public async Task GetSnapshot_SeedsTheLastIngestionFromPersistedData()
    {
        var project = await SeedProjectAsync("metrics-last-ingestion-project");
        var (db, service, _) = CreateService();

        var issue = NewIssue(project.Id, IssueStatus.Open, Severity.Error);
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        db.Events.Add(SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddMinutes(-1)));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        // The in-process counter is empty after a restart, the persisted instant fills the gap.
        snapshot.LastPersistedIngestionUtc.Should().NotBeNull();
        snapshot.LastPersistedIngestionUtc.Should().BeOnOrAfter(_now.AddMinutes(-1));
        snapshot.LastIngestionUtc.Should().Be(snapshot.LastPersistedIngestionUtc);
    }

    [Fact]
    public async Task GetSnapshot_KeepsTheLaterOfTheCounterAndThePersistedIngestion()
    {
        var (_, service, metrics) = CreateService();
        var counterValue = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        metrics.SetSnapshot(new SigilMetricsSnapshot(41, 7, counterValue));

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        snapshot.LastIngestionUtc.Should().Be(counterValue);
        snapshot.EventsIngested.Should().Be(41);
        snapshot.EventsDropped.Should().Be(7);
    }

    [Fact]
    public async Task GetSnapshot_ReportsANonEmptyBuildVersion()
    {
        var (_, service, _) = CreateService();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        snapshot.BuildVersion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetSnapshot_ReportsEveryConfiguredEventWindow()
    {
        var project = await SeedProjectAsync("metrics-windows-project");
        var (db, service, _) = CreateService();

        var issue = NewIssue(project.Id, IssueStatus.Open, Severity.Error);
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        foreach (TimeSpan window in MetricsDefaults.EventWindows)
            db.Events.Add(SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now - window + TimeSpan.FromSeconds(1)));

        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        snapshot.Events
            .Where(e => e.Project == "metrics-windows-project")
            .Select(e => e.Window)
            .Distinct()
            .Should().BeEquivalentTo(MetricsDefaults.EventWindows);
    }

    [Fact]
    public async Task GetSnapshot_ExposesNoIssueIdsInSamples()
    {
        var project = await SeedProjectAsync("metrics-cardinality-project");
        var (db, service, _) = CreateService();
        db.Issues.Add(NewIssue(project.Id, IssueStatus.Open, Severity.Error));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        var issueIds = await db.Issues
            .Where(i => i.ProjectId == project.Id)
            .Select(i => i.Id)
            .ToListAsync();

        List<IssueCountSample> samples = snapshot.Issues
            .Where(i => i.Project == "metrics-cardinality-project")
            .ToList();

        samples.Should().ContainSingle();
        samples[0].Count.Should().Be(1);
        samples[0].Project.Should().NotBe(issueIds[0].ToString());

        // The sample carries a project name and an enum, never a numeric identifier.
        samples.Should().NotContain(s => issueIds.Select(i => i.ToString()).Contains(s.Project));
    }

    private static Issue NewIssue(int projectId, IssueStatus status, Severity level, DateTime? firstSeen = null) =>
        new()
        {
            ProjectId = projectId,
            Fingerprint = Guid.NewGuid().ToString("N"),
            Status = status,
            Level = level,
            FirstSeen = firstSeen ?? _nowStatic,
            LastSeen = _nowStatic,
            LastChangedAt = _nowStatic
        };

    private static readonly DateTime _nowStatic = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static CapturedEvent SeedEventIn(SigilDbContext db, int projectId, int issueId, Severity level, DateTime timestamp)
    {
        var captured = new CapturedEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            ProjectId = projectId,
            IssueId = issueId,
            Timestamp = timestamp,
            ReceivedAt = timestamp,
            Level = level,
            Platform = Platform.CSharp,
            RawCompressedJson = []
        };
        db.Events.Add(captured);
        return captured;
    }

    private static RawEnvelope NewEnvelope(int projectId, string? error, DateTime? receivedAt = null) =>
        new()
        {
            ProjectId = projectId,
            RawData = "{}",
            ReceivedAt = receivedAt ?? _nowStatic,
            Error = error
        };

    private static string ClientReportEnvelope() =>
        "{\"event_id\":\"5d1b1a1e\"}\n{\"type\":\"client_report\",\"length\":2}\n{}";

    private static string TransactionEnvelope() =>
        "{\"event_id\":\"5d1b1a1f\"}\n{\"type\":\"transaction\",\"length\":2}\n{}";
}
