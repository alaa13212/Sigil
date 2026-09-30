using Sigil.Application.Interfaces;
using Sigil.Application.Models.Alerts;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class AlertServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static IAppConfigService StubAppConfig()
    {
        var svc = Substitute.For<IAppConfigService>();
        svc.HostUrl.Returns("https://sigil.test");
        return svc;
    }

    [Fact]
    public async Task CreateRule_PersistsAndReturnsResponse()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var service = new AlertService(ctx, StubDateTime(), [], StubAppConfig());

        var result = await service.CreateRuleAsync(project.Id, new CreateAlertRuleRequest(
            "Test Rule", AlertTrigger.NewIssue, channel.Id,
            ThresholdCount: null, ThresholdWindow: null, MinSeverity: Severity.Error,
            CooldownPeriod: TimeSpan.FromMinutes(15), Enabled: true));

        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be("Test Rule");
        result.Trigger.Should().Be(AlertTrigger.NewIssue);
        result.AlertChannelId.Should().Be(channel.Id);
    }

    [Fact]
    public async Task GetRulesForProject_ReturnsProjectRules()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var service = new AlertService(ctx, StubDateTime(), [], StubAppConfig());

        await service.CreateRuleAsync(project.Id, new("Rule 1", AlertTrigger.NewIssue, channel.Id));
        await service.CreateRuleAsync(project.Id, new("Rule 2", AlertTrigger.IssueRegression, channel.Id));

        var rules = await service.GetRulesForProjectAsync(project.Id);

        rules.Should().HaveCount(2);
        rules.Should().AllSatisfy(r => r.ProjectId.Should().Be(project.Id));
    }

    [Fact]
    public async Task UpdateRule_ModifiesFields()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var service = new AlertService(ctx, StubDateTime(), [], StubAppConfig());
        var created = await service.CreateRuleAsync(project.Id, new("Original", AlertTrigger.NewIssue, channel.Id));

        var updated = await service.UpdateRuleAsync(project.Id, created.Id, new UpdateAlertRuleRequest(
            "Renamed", AlertTrigger.IssueRegression, channel.Id,
            ThresholdCount: 10, ThresholdWindow: TimeSpan.FromHours(1),
            MinSeverity: Severity.Fatal, CooldownPeriod: TimeSpan.FromMinutes(5), Enabled: false));

        updated.Should().NotBeNull();
        updated.Name.Should().Be("Renamed");
        updated.Trigger.Should().Be(AlertTrigger.IssueRegression);
        updated.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRule_NonExistent_ReturnsNull()
    {
        await using var ctx = Ctx();
        var service = new AlertService(ctx, StubDateTime(), [], StubAppConfig());

        var result = await service.UpdateRuleAsync(0, 999999, new("x", AlertTrigger.NewIssue, 1,
            null, null, null, TimeSpan.FromMinutes(5), true));

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRule_RemovesFromDb()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var service = new AlertService(ctx, StubDateTime(), [], StubAppConfig());
        var created = await service.CreateRuleAsync(project.Id, new("ToDelete", AlertTrigger.NewIssue, channel.Id));

        var deleted = await service.DeleteRuleAsync(project.Id, created.Id);

        deleted.Should().BeTrue();

        await using var verifyCtx = Ctx();
        var inDb = await verifyCtx.AlertRules.FindAsync(created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRule_NonExistent_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var service = new AlertService(ctx, StubDateTime(), [], StubAppConfig());

        (await service.DeleteRuleAsync(0, 999999)).Should().BeFalse();
    }

    [Fact]
    public async Task ToggleRule_EnablesAndDisables()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var service = new AlertService(ctx, StubDateTime(), [], StubAppConfig());
        var created = await service.CreateRuleAsync(project.Id, new("Toggle", AlertTrigger.NewIssue, channel.Id, Enabled: true));

        (await service.ToggleRuleAsync(project.Id, created.Id, false)).Should().BeTrue();

        await using var verifyCtx = Ctx();
        var inDb = await verifyCtx.AlertRules.FindAsync(created.Id);
        inDb!.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleRule_NonExistent_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var service = new AlertService(ctx, StubDateTime(), [], StubAppConfig());

        (await service.ToggleRuleAsync(0, 999999, true)).Should().BeFalse();
    }

    // ── Evaluation ────────────────────────────────────────────────────────────

    private static IAlertSender StubSender(AlertChannelType type = AlertChannelType.Webhook, bool returns = true)
    {
        var s = Substitute.For<IAlertSender>();
        s.Channel.Returns(type);
        s.SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>()).Returns(returns);
        return s;
    }

    private static IDateTime StubDateTime(DateTime dt)
    {
        var stub = Substitute.For<IDateTime>();
        stub.UtcNow.Returns(dt);
        return stub;
    }

    [Fact]
    public async Task EvaluateNewIssue_MatchingRule_NoMinSev_FiresAlert()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.NewIssue;
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var sender = StubSender();
        var service = new AlertService(ctx, StubDateTime(), [sender], StubAppConfig());

        await service.EvaluateNewIssueAsync(issue);

        await sender.Received().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EvaluateNewIssue_NoMatchingRule_DoesNotFire()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        // No rules created for this project
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var sender = StubSender();
        var service = new AlertService(ctx, StubDateTime(), [sender], StubAppConfig());

        await service.EvaluateNewIssueAsync(issue);

        await sender.DidNotReceive().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EvaluateRegression_MatchingRule_FiresAlert()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.IssueRegression;
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var sender = StubSender();
        var service = new AlertService(ctx, StubDateTime(), [sender], StubAppConfig());

        await service.EvaluateRegressionAsync(issue);

        await sender.Received().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EvaluateThreshold_BelowCount_DoesNotFire()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.ThresholdExceeded;
        rule.ThresholdCount = 5;
        rule.ThresholdWindow = TimeSpan.FromHours(1);
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        // Only 2 events in window, threshold is 5
        for (int i = 0; i < 2; i++)
            await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddMinutes(-10));

        var sender = StubSender();
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(now);
        var service = new AlertService(ctx, dt, [sender], StubAppConfig());

        await service.EvaluateThresholdAsync(issue);

        await sender.DidNotReceive().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EvaluateThreshold_AtCount_Fires()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.ThresholdExceeded;
        rule.ThresholdCount = 3;
        rule.ThresholdWindow = TimeSpan.FromHours(1);
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        for (int i = 0; i < 3; i++)
            await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddMinutes(-10));

        var sender = StubSender();
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(now);
        var service = new AlertService(ctx, dt, [sender], StubAppConfig());

        await service.EvaluateThresholdAsync(issue);

        await sender.Received().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EvaluateThreshold_EventsOutsideWindow_NotCounted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.ThresholdExceeded;
        rule.ThresholdCount = 3;
        rule.ThresholdWindow = TimeSpan.FromHours(1);
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        // 3 events but outside the 1-hour window
        for (int i = 0; i < 3; i++)
            await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddHours(-2));

        var sender = StubSender();
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(now);
        var service = new AlertService(ctx, dt, [sender], StubAppConfig());

        await service.EvaluateThresholdAsync(issue);

        await sender.DidNotReceive().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EvaluateNewIssue_BelowMinSeverity_DoesNotFire()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.NewIssue;
        rule.MinSeverity = Severity.Fatal; // issue is Error, below Fatal
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id); // Level = Error
        var sender = StubSender();
        var service = new AlertService(ctx, StubDateTime(), [sender], StubAppConfig());

        await service.EvaluateNewIssueAsync(issue);

        await sender.DidNotReceive().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EvaluateNewIssue_MeetsMinSeverity_Fires()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.NewIssue;
        rule.MinSeverity = Severity.Warning; // issue is Error, meets Warning
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id); // Level = Error
        var sender = StubSender();
        var service = new AlertService(ctx, StubDateTime(), [sender], StubAppConfig());

        await service.EvaluateNewIssueAsync(issue);

        await sender.Received().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EvaluateRegression_BelowMinSeverity_DoesNotFire()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.IssueRegression;
        rule.MinSeverity = Severity.Fatal;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var sender = StubSender();
        var service = new AlertService(ctx, StubDateTime(), [sender], StubAppConfig());

        await service.EvaluateRegressionAsync(issue);

        await sender.DidNotReceive().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    // ── Cooldown ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FireAsync_CooldownActive_RecordsThrottled()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.NewIssue;
        rule.CooldownPeriod = TimeSpan.FromHours(1);
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        // Pre-insert a recent alert history entry
        ctx.AlertHistory.Add(new AlertHistory
        {
            AlertRuleId = rule.Id,
            IssueId = issue.Id,
            FiredAt = now.AddMinutes(-30), // within cooldown
            Status = AlertDeliveryStatus.Sent,
        });
        await ctx.SaveChangesAsync();

        var sender = StubSender();
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(now);
        var service = new AlertService(ctx, dt, [sender], StubAppConfig());

        await service.EvaluateNewIssueAsync(issue);

        await sender.DidNotReceive().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
        await using var verify = Ctx();
        verify.AlertHistory.Any(h => h.Status == AlertDeliveryStatus.Throttled).Should().BeTrue();
    }

    [Fact]
    public async Task FireAsync_CooldownExpired_Fires()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.NewIssue;
        rule.CooldownPeriod = TimeSpan.FromHours(1);
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        // Pre-insert an expired alert history entry
        ctx.AlertHistory.Add(new AlertHistory
        {
            AlertRuleId = rule.Id,
            IssueId = issue.Id,
            FiredAt = now.AddHours(-2), // outside cooldown
            Status = AlertDeliveryStatus.Sent,
        });
        await ctx.SaveChangesAsync();

        var sender = StubSender();
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(now);
        var service = new AlertService(ctx, dt, [sender], StubAppConfig());

        await service.EvaluateNewIssueAsync(issue);

        await sender.Received().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task FireAsync_SenderFailure_RecordsFailed()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.NewIssue;
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var sender = StubSender(returns: false);
        var service = new AlertService(ctx, StubDateTime(), [sender], StubAppConfig());

        await service.EvaluateNewIssueAsync(issue);

        await using var verify = Ctx();
        verify.AlertHistory.Any(h => h.Status == AlertDeliveryStatus.Failed).Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateNewIssue_InfoLevelIssue_DoesNotTriggerNewHighSeverityRule()
    {
        // Line 136: if (issue.Level.IsAtLeast(Severity.Error)) — Info is below Error, so NewHighSeverity rules must not fire.
        // With negation mutation: Info.IsAtLeast(Error) = false → negated = true → would incorrectly fire.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.NewHighSeverity;
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        issue.Level = Severity.Info;
        await ctx.SaveChangesAsync();

        var sender = StubSender();
        var service = new AlertService(ctx, StubDateTime(), [sender], StubAppConfig());

        await service.EvaluateNewIssueAsync(issue);

        await sender.DidNotReceive().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EvaluateThreshold_NullThresholdCount_DoesNotFire()
    {
        // Line 162: !ThresholdCount.HasValue || !ThresholdWindow.HasValue → continue (skip rule)
        // With || → && mutation: only skips when BOTH are null; here Count=null, Window=set → would not skip → crashes.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.ThresholdExceeded;
        rule.ThresholdCount = null;                    // null count — incomplete rule, must be skipped
        rule.ThresholdWindow = TimeSpan.FromHours(1);
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var sender = StubSender();
        var service = new AlertService(ctx, StubDateTime(), [sender], StubAppConfig());

        await service.EvaluateThresholdAsync(issue);

        await sender.DidNotReceive().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task EvaluateThreshold_NullThresholdWindow_DoesNotFire()
    {
        // Line 162: same || guard — ThresholdWindow null means rule is incomplete, must be skipped.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.ThresholdExceeded;
        rule.ThresholdCount = 5;
        rule.ThresholdWindow = null;                   // null window — incomplete rule, must be skipped
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var sender = StubSender();
        var service = new AlertService(ctx, StubDateTime(), [sender], StubAppConfig());

        await service.EvaluateThresholdAsync(issue);

        await sender.DidNotReceive().SendAsync(Arg.Any<AlertRule>(), Arg.Any<Issue>(), Arg.Any<string>());
    }

    [Fact]
    public async Task FireAsync_IssueWithPositiveId_SendsUrlContainingIssuePath()
    {
        // Lines 229/249: issue.Id > 0 ? $"{baseUrl}/…/{issue.Id}" : ""
        // Killing: > 0 → > 1 (Id=1 would produce empty URL and fail the Contains check)
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.NewIssue;
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var sender = StubSender();
        var service = new AlertService(ctx, StubDateTime(), [sender], StubAppConfig());

        await service.EvaluateNewIssueAsync(issue);

        await sender.Received().SendAsync(
            Arg.Any<AlertRule>(),
            Arg.Any<Issue>(),
            Arg.Is<string>(url => url.Contains($"/issues/{issue.Id}")));
    }

    [Fact]
    public async Task EvaluateNewIssue_NoMatchingSenderForChannel_ThrowsInvalidOperationException()
    {
        // Line 258: GetSender → FirstOrDefault ?? throw. No sender for the rule's channel type → must throw.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx); // Webhook channel
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.NewIssue;
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        // Pass a Slack sender, but the channel is Webhook — no match
        var slackSender = StubSender(AlertChannelType.Slack);
        var service = new AlertService(ctx, StubDateTime(), [slackSender], StubAppConfig());

        var act = () => service.EvaluateNewIssueAsync(issue);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No sender registered*");
    }

    // ── GetAlertHistory ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAlertHistory_ReturnsPaginated()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule = await TestHelper.CreateAlertRuleAsync(ctx, project.Id, channel.Id);
        rule.Trigger = AlertTrigger.NewIssue;
        rule.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 5; i++)
        {
            ctx.AlertHistory.Add(new AlertHistory
            {
                AlertRuleId = rule.Id,
                FiredAt = now.AddMinutes(-i),
                Status = AlertDeliveryStatus.Sent,
            });
        }
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var service = new AlertService(ctx2, StubDateTime(), [], StubAppConfig());
        var page1 = await service.GetAlertHistoryAsync(project.Id, page: 1, pageSize: 3);
        var page2 = await service.GetAlertHistoryAsync(project.Id, page: 2, pageSize: 3);

        page1.Items.Should().HaveCount(3);
        page2.Items.Count.Should().BeGreaterThanOrEqualTo(2);
        page1.TotalCount.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task GetAlertHistory_FilteredByProject()
    {
        await using var ctx = Ctx();
        var project1 = await TestHelper.CreateProjectAsync(ctx);
        var project2 = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var rule1 = await TestHelper.CreateAlertRuleAsync(ctx, project1.Id, channel.Id);
        var rule2 = await TestHelper.CreateAlertRuleAsync(ctx, project2.Id, channel.Id);
        rule1.Trigger = AlertTrigger.NewIssue;
        rule2.Trigger = AlertTrigger.NewIssue;
        rule1.MinSeverity = null;
        rule2.MinSeverity = null;
        await ctx.SaveChangesAsync();

        var now = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        ctx.AlertHistory.Add(new AlertHistory { AlertRuleId = rule1.Id, FiredAt = now, Status = AlertDeliveryStatus.Sent });
        ctx.AlertHistory.Add(new AlertHistory { AlertRuleId = rule2.Id, FiredAt = now, Status = AlertDeliveryStatus.Sent });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var service = new AlertService(ctx2, StubDateTime(), [], StubAppConfig());
        var result = await service.GetAlertHistoryAsync(project1.Id);

        result.Items.Should().OnlyContain(h => h.AlertRuleId == rule1.Id);
    }
}
