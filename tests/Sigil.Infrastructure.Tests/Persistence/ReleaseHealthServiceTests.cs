using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class ReleaseHealthServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);
    private static ReleaseHealthService Create(SigilDbContext ctx) => new(ctx);

    // ── GetReleaseHealth ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetReleaseHealth_Empty_ReturnsEmptyPage()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetReleaseHealthAsync(project.Id);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetReleaseHealth_AggregatesEventCountPerRelease()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1.0-{Guid.NewGuid():N}");
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        // Link events to the release
        for (int i = 0; i < 3; i++)
        {
            var ev = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
            ev.ReleaseId = release.Id;
        }
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetReleaseHealthAsync(project.Id);

        result.Items.Should().HaveCount(1);
        result.Items[0].TotalEvents.Should().Be(3);
    }

    [Fact]
    public async Task GetReleaseHealth_CountsDistinctAffectedIssues()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1.0-{Guid.NewGuid():N}");
        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var ev1 = await TestHelper.CreateEventAsync(ctx, project.Id, issue1.Id);
        ev1.ReleaseId = release.Id;
        var ev2 = await TestHelper.CreateEventAsync(ctx, project.Id, issue1.Id);
        ev2.ReleaseId = release.Id;
        var ev3 = await TestHelper.CreateEventAsync(ctx, project.Id, issue2.Id);
        ev3.ReleaseId = release.Id;
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetReleaseHealthAsync(project.Id);

        result.Items[0].AffectedIssues.Should().Be(2, "two distinct issues");
    }

    [Fact]
    public async Task GetReleaseHealth_Paginated_ReturnsCorrectSlice()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var now = DateTime.UtcNow;
        var uid = Guid.NewGuid().ToString("N")[..8];
        for (int i = 1; i <= 5; i++)
            await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v{i}.0-{uid}", firstSeen: now.AddDays(-i));

        await using var ctx2 = Ctx();
        var page1 = await Create(ctx2).GetReleaseHealthAsync(project.Id, page: 1, pageSize: 2);
        var page2 = await Create(ctx2).GetReleaseHealthAsync(project.Id, page: 2, pageSize: 2);

        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(5);
        page1.Items.Select(r => r.Id).Should().NotIntersectWith(page2.Items.Select(r => r.Id));
    }

    // ── GetReleaseDetail ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetReleaseDetail_NotFound_ReturnsNull()
    {
        await using var ctx = Ctx();
        var result = await Create(ctx).GetReleaseDetailAsync(999999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetReleaseDetail_ReturnsTopIssues()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1.0-{Guid.NewGuid():N}");
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var ev = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        ev.ReleaseId = release.Id;
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetReleaseDetailAsync(release.Id);

        result.Should().NotBeNull();
        result!.TopIssues.Should().HaveCount(1);
        result.TopIssues[0].IssueId.Should().Be(issue.Id);
    }

    [Fact]
    public async Task GetReleaseDetail_MarksIssuesNewIfFirstSeenAfterRelease()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1.0-{Guid.NewGuid():N}", firstSeen: releaseDate);

        // Issue that existed before the release
        var oldIssue = await TestHelper.CreateIssueAsync(ctx, project.Id, "Old Issue");
        oldIssue.FirstSeen = releaseDate.AddDays(-5);

        // Issue that appeared after the release
        var newIssue = await TestHelper.CreateIssueAsync(ctx, project.Id, "New Issue");
        newIssue.FirstSeen = releaseDate.AddDays(1);
        await ctx.SaveChangesAsync();

        var evOld = await TestHelper.CreateEventAsync(ctx, project.Id, oldIssue.Id, timestamp: releaseDate.AddDays(1));
        evOld.ReleaseId = release.Id;
        var evNew = await TestHelper.CreateEventAsync(ctx, project.Id, newIssue.Id, timestamp: releaseDate.AddDays(1));
        evNew.ReleaseId = release.Id;
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetReleaseDetailAsync(release.Id);

        result.Should().NotBeNull();
        var oldEntry = result!.TopIssues.First(i => i.IssueId == oldIssue.Id);
        var newEntry = result.TopIssues.First(i => i.IssueId == newIssue.Id);
        oldEntry.IsNew.Should().BeFalse("existed before the release");
        newEntry.IsNew.Should().BeTrue("first seen after release date");
    }
}
