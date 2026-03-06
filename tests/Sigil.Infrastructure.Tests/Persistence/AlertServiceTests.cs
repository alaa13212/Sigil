using Microsoft.EntityFrameworkCore;
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

        var updated = await service.UpdateRuleAsync(created.Id, new UpdateAlertRuleRequest(
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

        var result = await service.UpdateRuleAsync(999999, new("x", AlertTrigger.NewIssue, 1,
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

        var deleted = await service.DeleteRuleAsync(created.Id);

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

        (await service.DeleteRuleAsync(999999)).Should().BeFalse();
    }

    [Fact]
    public async Task ToggleRule_EnablesAndDisables()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var channel = await TestHelper.CreateAlertChannelAsync(ctx);
        var service = new AlertService(ctx, StubDateTime(), [], StubAppConfig());
        var created = await service.CreateRuleAsync(project.Id, new("Toggle", AlertTrigger.NewIssue, channel.Id, Enabled: true));

        (await service.ToggleRuleAsync(created.Id, false)).Should().BeTrue();

        await using var verifyCtx = Ctx();
        var inDb = await verifyCtx.AlertRules.FindAsync(created.Id);
        inDb!.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleRule_NonExistent_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var service = new AlertService(ctx, StubDateTime(), [], StubAppConfig());

        (await service.ToggleRuleAsync(999999, true)).Should().BeFalse();
    }
}
