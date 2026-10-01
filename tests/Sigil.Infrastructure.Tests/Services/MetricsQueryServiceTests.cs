using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Metrics;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;
using Sigil.Infrastructure.Tests.Fixtures;

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
        snapshot.Issues.Should().Contain(i => i.Project == "metrics-issues-project" && i.Status == IssueStatus.Resolved && i.Count == 1);
    }

    [Fact]
    public async Task GetSnapshot_ExcludesEventsOlderThanTheWindow()
    {
        var project = await SeedProjectAsync("metrics-window-project");
        var (db, service, _) = CreateService();

        var issue = NewIssue(project.Id, IssueStatus.Open, Severity.Error);
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        db.Events.AddRange(
            SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddHours(-1)),
            SeedEventIn(db, project.Id, issue.Id, Severity.Error, _now.AddHours(-48)));
        await db.SaveChangesAsync();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        snapshot.Events.Single(e => e.Project == "metrics-window-project" && e.Level == Severity.Error)
            .Count.Should().Be(1);
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
    public async Task GetSnapshot_CarriesTheCounterValues()
    {
        var (_, service, metrics) = CreateService();
        var ingestedAt = new DateTime(2026, 9, 30, 8, 0, 0, DateTimeKind.Utc);
        metrics.SetSnapshot(new SigilMetricsSnapshot(41, 7, ingestedAt));

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        snapshot.EventsIngested.Should().Be(41);
        snapshot.EventsDropped.Should().Be(7);
        snapshot.LastIngestionUtc.Should().Be(ingestedAt);
    }

    [Fact]
    public async Task GetSnapshot_ReportsANonEmptyBuildVersion()
    {
        var (_, service, _) = CreateService();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        snapshot.BuildVersion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetSnapshot_ReportsTheConfiguredEventWindow()
    {
        var (_, service, _) = CreateService();

        MetricsSnapshot snapshot = await service.GetSnapshotAsync();

        snapshot.EventWindow.Should().Be(MetricsDefaults.EventWindow);
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

    private static Issue NewIssue(int projectId, IssueStatus status, Severity level) =>
        new()
        {
            ProjectId = projectId,
            Fingerprint = Guid.NewGuid().ToString("N"),
            Status = status,
            Level = level,
            FirstSeen = _nowStatic,
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

    private static RawEnvelope NewEnvelope(int projectId, string? error) =>
        new()
        {
            ProjectId = projectId,
            RawData = "{}",
            ReceivedAt = _nowStatic,
            Error = error
        };
}
