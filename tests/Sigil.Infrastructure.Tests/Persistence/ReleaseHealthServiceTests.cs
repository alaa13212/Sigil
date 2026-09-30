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

    [Fact]
    public async Task GetReleaseHealth_OrderedByFirstSeenDescending()
    {
        // Line 16: OrderByDescending(r => r.FirstSeenAt) — newest release must appear first.
        // With ascending mutation: oldest would appear first.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var now = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var older = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1-{Guid.NewGuid():N}", firstSeen: now.AddDays(-2));
        var newer = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v2-{Guid.NewGuid():N}", firstSeen: now);

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetReleaseHealthAsync(project.Id);

        var ids = result.Items.Select(r => r.Id).ToList();
        ids.IndexOf(newer.Id).Should().BeLessThan(ids.IndexOf(older.Id), "newer release must appear first");
    }

    [Fact]
    public async Task GetReleaseHealth_IssueFirstSeenExactlyAtRelease_CountedAsNew()
    {
        // Line 24: i.FirstSeen >= r.FirstSeenAt (inclusive boundary).
        // With > mutation: issue at exactly the release date would not be counted as new.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1-{Guid.NewGuid():N}", firstSeen: releaseDate);

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        issue.FirstSeen = releaseDate; // exactly at the release boundary
        await ctx.SaveChangesAsync();

        var ev = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: releaseDate.AddDays(1));
        ev.ReleaseId = release.Id;
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetReleaseHealthAsync(project.Id);

        result.Items.Should().HaveCount(1);
        result.Items[0].NewIssues.Should().Be(1, "issue with FirstSeen == release date must count as new");
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
    public async Task GetReleaseDetail_IssueFirstSeenExactlyAtRelease_IsNew()
    {
        // Line 74: issue.FirstSeen >= data.FirstSeenAt (inclusive boundary for displayed IsNew flag).
        // With > mutation: issue with FirstSeen == release date would incorrectly show IsNew=false.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1-{Guid.NewGuid():N}", firstSeen: releaseDate);

        var boundaryIssue = await TestHelper.CreateIssueAsync(ctx, project.Id, "Boundary");
        boundaryIssue.FirstSeen = releaseDate; // exactly at release date
        await ctx.SaveChangesAsync();

        var ev = await TestHelper.CreateEventAsync(ctx, project.Id, boundaryIssue.Id, timestamp: releaseDate.AddDays(1));
        ev.ReleaseId = release.Id;
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetReleaseDetailAsync(release.Id);

        result.Should().NotBeNull();
        var entry = result!.TopIssues.Should().ContainSingle().Which;
        entry.IsNew.Should().BeTrue("FirstSeen == release date qualifies as a new issue (>= boundary)");
    }

    [Fact]
    public async Task GetReleaseDetail_TopIssues_NewIssuesOrderedBeforeOld()
    {
        // Lines 54-55: OrderByDescending(IsNew).ThenByDescending(EventCount) — new issues must come first.
        // With ascending mutation: old issues would appear first.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1-{Guid.NewGuid():N}", firstSeen: releaseDate);

        // Old issue: existed before release
        var oldIssue = await TestHelper.CreateIssueAsync(ctx, project.Id, "Old");
        oldIssue.FirstSeen = releaseDate.AddDays(-5);
        // New issue: appeared after release
        var newIssue = await TestHelper.CreateIssueAsync(ctx, project.Id, "New");
        newIssue.FirstSeen = releaseDate.AddDays(1);
        await ctx.SaveChangesAsync();

        // oldIssue: first event at exactly release date → Min > release = false → IsNew=false in ordering
        var ev1 = await TestHelper.CreateEventAsync(ctx, project.Id, oldIssue.Id, timestamp: releaseDate);
        ev1.ReleaseId = release.Id;
        // newIssue: first event strictly after release date → Min > release = true → IsNew=true in ordering
        var ev2 = await TestHelper.CreateEventAsync(ctx, project.Id, newIssue.Id, timestamp: releaseDate.AddDays(1));
        ev2.ReleaseId = release.Id;
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetReleaseDetailAsync(release.Id);

        result.Should().NotBeNull();
        result!.TopIssues.Should().HaveCount(2);
        result.TopIssues[0].IssueId.Should().Be(newIssue.Id, "new issue should be ordered first");
        result.TopIssues[1].IssueId.Should().Be(oldIssue.Id);
    }

    [Fact]
    public async Task GetReleaseDetail_FirstEventAtExactlyReleaseDate_NotCountedAsNewInOrdering()
    {
        // Line 53: g.Min(e => e.Timestamp) > r.FirstSeenAt (strict — event at release time is NOT new for ordering)
        // With >= mutation: the issue would be ordered as "new" even though its first event is at release time.
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseDate = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"v1-{Guid.NewGuid():N}", firstSeen: releaseDate);

        // Issue with many events but first event exactly at release date — not "new" in TopIssueStats ordering
        var existingIssue = await TestHelper.CreateIssueAsync(ctx, project.Id, "Existing");
        existingIssue.FirstSeen = releaseDate.AddDays(-10); // existed before release
        // Truly new issue with first event after release date but fewer events
        var newIssue = await TestHelper.CreateIssueAsync(ctx, project.Id, "Truly New");
        newIssue.FirstSeen = releaseDate.AddDays(1);
        await ctx.SaveChangesAsync();

        // Existing issue: 5 events, first at exactly release date
        for (int i = 0; i < 5; i++)
        {
            var ev = await TestHelper.CreateEventAsync(ctx, project.Id, existingIssue.Id,
                timestamp: i == 0 ? releaseDate : releaseDate.AddDays(i));
            ev.ReleaseId = release.Id;
        }
        // New issue: 1 event strictly after release date
        var newEv = await TestHelper.CreateEventAsync(ctx, project.Id, newIssue.Id, timestamp: releaseDate.AddDays(1));
        newEv.ReleaseId = release.Id;
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetReleaseDetailAsync(release.Id);

        result.Should().NotBeNull();
        // newIssue has IsNew=true in ordering → appears first; existingIssue has IsNew=false → second despite 5 events
        result!.TopIssues[0].IssueId.Should().Be(newIssue.Id, "truly new issue (first event > release date) orders first");
        result.TopIssues[1].IssueId.Should().Be(existingIssue.Id);
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
