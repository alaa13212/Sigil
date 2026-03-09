using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;
using Sigil.Infrastructure.Tests.Persistence;
using Sigil.Infrastructure.Workers;

namespace Sigil.Infrastructure.Tests.Services;

[Collection(DbCollection)]
public class RetentionWorkerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private IAppConfigService StubAppConfig(
        int maxAgeDays = 30,
        int maxEvents = 10000,
        int failedEnvelopeMaxAgeDays = 7,
        int intervalMinutes = 60)
    {
        var cfg = Substitute.For<IAppConfigService>();
        cfg.RetentionDefaultMaxAgeDays.Returns(maxAgeDays);
        cfg.RetentionDefaultMaxEvents.Returns(maxEvents);
        cfg.RetentionFailedEnvelopeMaxAgeDays.Returns(failedEnvelopeMaxAgeDays);
        cfg.RetentionCheckIntervalMinutes.Returns(intervalMinutes);
        return cfg;
    }

    private static IProjectConfigService StubProjectConfig(int? maxAgeDays = null, int? maxEventCount = null)
    {
        var cfg = Substitute.For<IProjectConfigService>();
        cfg.RetentionMaxAgeDays(Arg.Any<int>()).Returns(maxAgeDays);
        cfg.RetentionMaxEventCount(Arg.Any<int>()).Returns(maxEventCount);
        return cfg;
    }

    /// <summary>Invokes a private method on RetentionWorker, appending CancellationToken.None to the args.</summary>
    private static Task InvokeAsync(RetentionWorker worker, string method, params object[] args)
    {
        var mi = typeof(RetentionWorker).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(RetentionWorker), method);
        var fullArgs = args.Append((object)CancellationToken.None).ToArray();
        return (Task)mi.Invoke(worker, fullArgs)!;
    }

    private RetentionWorker CreateWorker(IAppConfigService? appConfig = null, IProjectConfigService? projectConfig = null)
    {
        // Provide a real DbContext via ServiceProvider so EnforceRetentionAsync can resolve it
        var services = new ServiceCollection();
        services.AddScoped(_ => Ctx());
        var sp = services.BuildServiceProvider();

        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(DateTime.UtcNow);
        return new RetentionWorker(
            sp,
            appConfig ?? StubAppConfig(),
            projectConfig ?? StubProjectConfig(),
            dt,
            NullLogger<RetentionWorker>.Instance);
    }

    // ── CleanExpiredSharedLinksAsync ──────────────────────────────────────────

    [Fact]
    public async Task CleanLinks_ExpiredToken_Removed()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        var link = new SharedIssueLink
        {
            Token = Guid.NewGuid(),
            IssueId = issue.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = DateTime.UtcNow.AddHours(-1), // already expired
        };
        ctx.SharedIssueLinks.Add(link);
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var worker = CreateWorker();
        await InvokeAsync(worker, "CleanExpiredSharedLinksAsync", ctx2);

        await using var verifyCtx = Ctx();
        verifyCtx.SharedIssueLinks.Any(l => l.Token == link.Token).Should().BeFalse();
    }

    [Fact]
    public async Task CleanLinks_ActiveToken_NotRemoved()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var user = await TestHelper.CreateUserAsync(ctx);
        var link = new SharedIssueLink
        {
            Token = Guid.NewGuid(),
            IssueId = issue.Id,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7), // still active
        };
        ctx.SharedIssueLinks.Add(link);
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var worker = CreateWorker();
        await InvokeAsync(worker, "CleanExpiredSharedLinksAsync", ctx2);

        await using var verifyCtx = Ctx();
        verifyCtx.SharedIssueLinks.Any(l => l.Token == link.Token).Should().BeTrue();
    }

    // ── CleanFailedEnvelopesAsync ─────────────────────────────────────────────

    [Fact]
    public async Task CleanEnvelopes_OldFailedEnvelope_Removed()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var envelope = new RawEnvelope
        {
            ProjectId = project.Id,
            RawData = "{}",
            ReceivedAt = DateTime.UtcNow.AddDays(-10), // older than 7-day cutoff
            Error = "Parse error",
        };
        ctx.RawEnvelopes.Add(envelope);
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var worker = CreateWorker(appConfig: StubAppConfig(failedEnvelopeMaxAgeDays: 7));
        await InvokeAsync(worker, "CleanFailedEnvelopesAsync", ctx2);

        await using var verifyCtx = Ctx();
        verifyCtx.RawEnvelopes.Any(r => r.Id == envelope.Id).Should().BeFalse();
    }

    [Fact]
    public async Task CleanEnvelopes_RecentFailedEnvelope_NotRemoved()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var envelope = new RawEnvelope
        {
            ProjectId = project.Id,
            RawData = "{}",
            ReceivedAt = DateTime.UtcNow.AddDays(-1), // within 7-day cutoff
            Error = "Parse error",
        };
        ctx.RawEnvelopes.Add(envelope);
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var worker = CreateWorker(appConfig: StubAppConfig(failedEnvelopeMaxAgeDays: 7));
        await InvokeAsync(worker, "CleanFailedEnvelopesAsync", ctx2);

        await using var verifyCtx = Ctx();
        verifyCtx.RawEnvelopes.Any(r => r.Id == envelope.Id).Should().BeTrue();
    }

    [Fact]
    public async Task CleanEnvelopes_SuccessfulEnvelope_NotRemoved()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var envelope = new RawEnvelope
        {
            ProjectId = project.Id,
            RawData = "{}",
            ReceivedAt = DateTime.UtcNow.AddDays(-30),
            Error = null, // successful — no error
        };
        ctx.RawEnvelopes.Add(envelope);
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var worker = CreateWorker(appConfig: StubAppConfig(failedEnvelopeMaxAgeDays: 7));
        await InvokeAsync(worker, "CleanFailedEnvelopesAsync", ctx2);

        await using var verifyCtx = Ctx();
        verifyCtx.RawEnvelopes.Any(r => r.Id == envelope.Id).Should().BeTrue();
    }

    // ── EnforceAgeRetentionAsync ──────────────────────────────────────────────

    [Fact]
    public async Task AgeRetention_EventOlderThanCutoff_Deleted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        // Event is 40 days old, cutoff is 30 days
        var oldEvent = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id,
            timestamp: DateTime.UtcNow.AddDays(-40));

        await using var ctx2 = Ctx();
        var worker = CreateWorker(appConfig: StubAppConfig(maxAgeDays: 30));
        await InvokeAsync(worker, "EnforceAgeRetentionAsync", ctx2, project.Id);

        await using var verifyCtx = Ctx();
        verifyCtx.Events.Any(e => e.Id == oldEvent.Id).Should().BeFalse();
    }

    [Fact]
    public async Task AgeRetention_RecentEvent_NotDeleted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var recentEvent = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id,
            timestamp: DateTime.UtcNow.AddDays(-5));

        await using var ctx2 = Ctx();
        var worker = CreateWorker(appConfig: StubAppConfig(maxAgeDays: 30));
        await InvokeAsync(worker, "EnforceAgeRetentionAsync", ctx2, project.Id);

        await using var verifyCtx = Ctx();
        verifyCtx.Events.Any(e => e.Id == recentEvent.Id).Should().BeTrue();
    }

    [Fact]
    public async Task AgeRetention_OnlyAffectsCorrectProject()
    {
        await using var ctx = Ctx();
        var project1 = await TestHelper.CreateProjectAsync(ctx);
        var project2 = await TestHelper.CreateProjectAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project1.Id);
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project2.Id);
        var oldEvent1 = await TestHelper.CreateEventAsync(ctx, project1.Id, issue1.Id, timestamp: DateTime.UtcNow.AddDays(-40));
        var oldEvent2 = await TestHelper.CreateEventAsync(ctx, project2.Id, issue2.Id, timestamp: DateTime.UtcNow.AddDays(-40));

        await using var ctx2 = Ctx();
        var worker = CreateWorker(appConfig: StubAppConfig(maxAgeDays: 30));
        // Only enforce for project1
        await InvokeAsync(worker, "EnforceAgeRetentionAsync", ctx2, project1.Id);

        await using var verifyCtx = Ctx();
        verifyCtx.Events.Any(e => e.Id == oldEvent1.Id).Should().BeFalse("project1's old event should be deleted");
        verifyCtx.Events.Any(e => e.Id == oldEvent2.Id).Should().BeTrue("project2's event should be untouched");
    }

    [Fact]
    public async Task AgeRetention_ProjectOverrideTakesPrecedence()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        // Event is 20 days old — older than project override (10 days), newer than global (30 days)
        var evt = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id,
            timestamp: DateTime.UtcNow.AddDays(-20));

        await using var ctx2 = Ctx();
        var worker = CreateWorker(
            appConfig: StubAppConfig(maxAgeDays: 30),
            projectConfig: StubProjectConfig(maxAgeDays: 10)); // project override is stricter
        await InvokeAsync(worker, "EnforceAgeRetentionAsync", ctx2, project.Id);

        await using var verifyCtx = Ctx();
        verifyCtx.Events.Any(e => e.Id == evt.Id).Should().BeFalse("project override (10d) should delete the 20-day-old event");
    }

    // ── EnforceCountRetentionAsync ────────────────────────────────────────────

    [Fact]
    public async Task CountRetention_ExcessEvents_OldestDeleted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var now = DateTime.UtcNow;
        // Create 5 events, oldest first
        for (int i = 0; i < 5; i++)
            await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: now.AddMinutes(-i * 10));

        await using var ctx2 = Ctx();
        var worker = CreateWorker(appConfig: StubAppConfig(maxEvents: 3));
        await InvokeAsync(worker, "EnforceCountRetentionAsync", ctx2, project.Id);

        await using var verifyCtx = Ctx();
        verifyCtx.Events.Count(e => e.ProjectId == project.Id).Should().Be(3);
    }

    [Fact]
    public async Task CountRetention_UnderLimit_NothingDeleted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        for (int i = 0; i < 3; i++)
            await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);

        await using var ctx2 = Ctx();
        var worker = CreateWorker(appConfig: StubAppConfig(maxEvents: 10));
        await InvokeAsync(worker, "EnforceCountRetentionAsync", ctx2, project.Id);

        await using var verifyCtx = Ctx();
        verifyCtx.Events.Count(e => e.ProjectId == project.Id).Should().Be(3);
    }

    [Fact]
    public async Task CountRetention_OnlyAffectsCorrectProject()
    {
        await using var ctx = Ctx();
        var project1 = await TestHelper.CreateProjectAsync(ctx);
        var project2 = await TestHelper.CreateProjectAsync(ctx);
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project1.Id);
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project2.Id);
        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
            await TestHelper.CreateEventAsync(ctx, project1.Id, issue1.Id, timestamp: now.AddMinutes(-i));
        for (int i = 0; i < 5; i++)
            await TestHelper.CreateEventAsync(ctx, project2.Id, issue2.Id, timestamp: now.AddMinutes(-i));

        await using var ctx2 = Ctx();
        var worker = CreateWorker(appConfig: StubAppConfig(maxEvents: 3));
        await InvokeAsync(worker, "EnforceCountRetentionAsync", ctx2, project1.Id);

        await using var verifyCtx = Ctx();
        verifyCtx.Events.Count(e => e.ProjectId == project1.Id).Should().Be(3);
        verifyCtx.Events.Count(e => e.ProjectId == project2.Id).Should().Be(5, "project2 should be unaffected");
    }
}
