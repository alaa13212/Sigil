using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.IssueTrackers;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;
using Sigil.Infrastructure.Tests.Fixtures;
using Sigil.Infrastructure.Tests.Persistence;
using Sigil.Infrastructure.Workers;

namespace Sigil.Infrastructure.Tests.Workers;

[Collection(DbCollection)]
public class IssueTrackerSyncWorkerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    // Share one provider per test so all encrypt/decrypt calls within a test use the same key
    private readonly EphemeralDataProtectionProvider _dpProvider = new();

    private TokenEncryptionService CreateEncryption() => new(_dpProvider);

    private static Task InvokeSyncAllAsync(IssueTrackerSyncWorker worker)
    {
        var mi = typeof(IssueTrackerSyncWorker)
            .GetMethod("SyncAllAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(IssueTrackerSyncWorker), "SyncAllAsync");
        return (Task)mi.Invoke(worker, [CancellationToken.None])!;
    }

    private IssueTrackerSyncWorker CreateWorker(IIssueTrackerClient? mockClient = null)
    {
        var clients = mockClient != null
            ? (IEnumerable<IIssueTrackerClient>)[mockClient]
            : [];
        var services = new ServiceCollection();
        services.AddScoped(_ => Ctx());
        services.AddScoped(_ => CreateEncryption());
        services.AddSingleton(clients);
        var sp = services.BuildServiceProvider();

        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc));
        return new IssueTrackerSyncWorker(sp, dt, NullLogger<IssueTrackerSyncWorker>.Instance);
    }

    private async Task<IssueTrackerConfig> CreateConfigAsync(
        SigilDbContext ctx,
        int projectId,
        TrackerType type = TrackerType.GitHub,
        bool twoWaySync = true,
        bool enabled = true)
    {
        var config = new IssueTrackerConfig
        {
            ProjectId = projectId,
            TrackerType = type,
            EncryptedConfig = CreateEncryption().Encrypt("""{"owner":"acme","repo":"app","pat":"tok"}"""),
            Enabled = enabled,
            TwoWaySync = twoWaySync,
            CreatedAt = DateTime.UtcNow,
        };
        ctx.IssueTrackerConfigs.Add(config);
        await ctx.SaveChangesAsync();
        return config;
    }

    private static async Task<ExternalIssueLink> CreateLinkAsync(
        SigilDbContext ctx,
        int issueId,
        TrackerType type = TrackerType.GitHub,
        string externalId = "42",
        string externalStatus = "open")
    {
        var link = new ExternalIssueLink
        {
            IssueId = issueId,
            TrackerType = type,
            ExternalId = externalId,
            ExternalUrl = "https://github.com/acme/app/issues/42",
            ExternalStatus = externalStatus,
            CreatedAt = DateTime.UtcNow,
        };
        ctx.ExternalIssueLinks.Add(link);
        await ctx.SaveChangesAsync();
        return link;
    }

    private static IIssueTrackerClient MockClient(
        string statusString,
        bool isCompleted,
        TrackerType type = TrackerType.GitHub)
    {
        var client = Substitute.For<IIssueTrackerClient>();
        client.TrackerType.Returns(type);
        client.GetStatusAsync(Arg.Any<TrackerConfig>(), Arg.Any<string>())
            .Returns(new ExternalIssueStatus("42", statusString, isCompleted));
        client.CloseIssueAsync(Arg.Any<TrackerConfig>(), Arg.Any<string>())
            .Returns(true);
        return client;
    }

    // ── External closed → Sigil issue resolved ────────────────────────────────

    [Fact]
    public async Task SyncAll_ExternalClosed_ResolvesSigilIssue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        issue.Status = IssueStatus.Open;
        await ctx.SaveChangesAsync();

        await CreateConfigAsync(ctx, project.Id, twoWaySync: true);
        await CreateLinkAsync(ctx, issue.Id, externalStatus: "open");

        // External is now "closed" / completed
        var client = MockClient("closed", isCompleted: true);
        var worker = CreateWorker(client);

        await InvokeSyncAllAsync(worker);

        await using var verify = Ctx();
        var updated = verify.Issues.First(i => i.Id == issue.Id);
        updated.Status.Should().Be(IssueStatus.Resolved);

        var link = verify.ExternalIssueLinks.First(l => l.IssueId == issue.Id);
        link.ExternalStatus.Should().Be("closed");
        link.LastSyncedAt.Should().Be(new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    // ── Sigil resolved → external issue closed ───────────────────────────────

    [Fact]
    public async Task SyncAll_SigilResolved_ClosesExternalIssue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        issue.Status = IssueStatus.Resolved;
        await ctx.SaveChangesAsync();

        await CreateConfigAsync(ctx, project.Id, twoWaySync: true);
        await CreateLinkAsync(ctx, issue.Id, externalStatus: "open");

        // External is still open; first call returns open, after close returns closed
        var client = Substitute.For<IIssueTrackerClient>();
        client.TrackerType.Returns(TrackerType.GitHub);
        client.GetStatusAsync(Arg.Any<TrackerConfig>(), Arg.Any<string>())
            .Returns(
                new ExternalIssueStatus("42", "open", false),    // first call: still open
                new ExternalIssueStatus("42", "closed", true));  // second call: after close
        client.CloseIssueAsync(Arg.Any<TrackerConfig>(), Arg.Any<string>()).Returns(true);

        var worker = CreateWorker(client);
        await InvokeSyncAllAsync(worker);

        await client.Received(1).CloseIssueAsync(Arg.Any<TrackerConfig>(), "42");

        await using var verify = Ctx();
        var link = verify.ExternalIssueLinks.First(l => l.IssueId == issue.Id);
        link.ExternalStatus.Should().Be("closed");
    }

    // ── No two-way sync → links not synced ───────────────────────────────────

    [Fact]
    public async Task SyncAll_TwoWaySyncDisabled_SkipsLinks()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        issue.Status = IssueStatus.Open;
        await ctx.SaveChangesAsync();

        await CreateConfigAsync(ctx, project.Id, twoWaySync: false);  // two-way sync OFF
        await CreateLinkAsync(ctx, issue.Id, externalStatus: "open");

        var client = Substitute.For<IIssueTrackerClient>();
        client.TrackerType.Returns(TrackerType.GitHub);

        var worker = CreateWorker(client);
        await InvokeSyncAllAsync(worker);

        // Client should never be called because TwoWaySync=false filters out the link
        await client.DidNotReceive().GetStatusAsync(Arg.Any<TrackerConfig>(), Arg.Any<string>());
    }

    // ── Config disabled → links not synced ───────────────────────────────────

    [Fact]
    public async Task SyncAll_ConfigDisabled_SkipsLinks()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        await CreateConfigAsync(ctx, project.Id, twoWaySync: true, enabled: false);
        await CreateLinkAsync(ctx, issue.Id);

        var client = Substitute.For<IIssueTrackerClient>();
        client.TrackerType.Returns(TrackerType.GitHub);

        var worker = CreateWorker(client);
        await InvokeSyncAllAsync(worker);

        await client.DidNotReceive().GetStatusAsync(Arg.Any<TrackerConfig>(), Arg.Any<string>());
    }

    // ── External still open, Sigil open → no change ──────────────────────────

    [Fact]
    public async Task SyncAll_ExternalOpen_SigilOpen_NoChange()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        issue.Status = IssueStatus.Open;
        await ctx.SaveChangesAsync();

        await CreateConfigAsync(ctx, project.Id, twoWaySync: true);
        await CreateLinkAsync(ctx, issue.Id, externalStatus: "open");

        var client = MockClient("open", isCompleted: false);
        var worker = CreateWorker(client);
        await InvokeSyncAllAsync(worker);

        await using var verify = Ctx();
        var updated = verify.Issues.First(i => i.Id == issue.Id);
        updated.Status.Should().Be(IssueStatus.Open, "no sync direction triggered");
    }

    // ── LastSyncedAt is stamped ───────────────────────────────────────────────

    [Fact]
    public async Task SyncAll_AlwaysStampsLastSyncedAt()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        await CreateConfigAsync(ctx, project.Id, twoWaySync: true);
        await CreateLinkAsync(ctx, issue.Id, externalStatus: "open");

        var client = MockClient("open", isCompleted: false);
        var worker = CreateWorker(client);
        await InvokeSyncAllAsync(worker);

        await using var verify = Ctx();
        var link = verify.ExternalIssueLinks.First(l => l.IssueId == issue.Id);
        link.LastSyncedAt.Should().Be(new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc));
    }
}
