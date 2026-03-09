using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class BookmarkServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime(DateTime? dt = null)
    {
        var stub = Substitute.For<IDateTime>();
        stub.UtcNow.Returns(dt ?? new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        return stub;
    }

    private static IIssueActivityLogger StubActivityLogger()
    {
        var logger = Substitute.For<IIssueActivityLogger>();
        logger.LogActivityAsync(Arg.Any<int>(), Arg.Any<IssueActivityAction>(), Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>())
            .Returns(new IssueActivity());
        return logger;
    }

    private BookmarkService Create(SigilDbContext ctx, IDateTime? dt = null, IIssueActivityLogger? logger = null)
        => new(ctx, dt ?? StubDateTime(), logger ?? StubActivityLogger());

    // ── Toggle ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Toggle_NotBookmarked_BookmarksAndSetsTimestamp()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var fixedTime = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var service = Create(ctx, StubDateTime(fixedTime));

        var result = await service.ToggleBookmarkAsync(issue.Id, user.Id);

        result.Should().BeTrue();
        await using var verify = Ctx();
        var state = await verify.UserIssueStates.FindAsync(user.Id, issue.Id);
        state.Should().NotBeNull();
        state!.IsBookmarked.Should().BeTrue();
        state.BookmarkedAt.Should().Be(fixedTime);
    }

    [Fact]
    public async Task Toggle_AlreadyBookmarked_RemovesBookmarkAndClearsTimestamp()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        ctx.UserIssueStates.Add(new UserIssueState
        {
            UserId = user.Id, IssueId = issue.Id,
            IsBookmarked = true, BookmarkedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var service = Create(ctx2);
        var result = await service.ToggleBookmarkAsync(issue.Id, user.Id);

        result.Should().BeFalse();
        await using var verify = Ctx();
        var state = await verify.UserIssueStates.FindAsync(user.Id, issue.Id);
        state!.IsBookmarked.Should().BeFalse();
        state.BookmarkedAt.Should().BeNull();
    }

    [Fact]
    public async Task Toggle_BookmarkAdded_LogsActivity()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var logger = StubActivityLogger();
        var service = Create(ctx, logger: logger);

        await service.ToggleBookmarkAsync(issue.Id, user.Id);

        await logger.Received(1).LogActivityAsync(issue.Id, IssueActivityAction.Bookmarked, user.Id, Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());
    }

    [Fact]
    public async Task Toggle_BookmarkRemoved_DoesNotLogActivity()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        ctx.UserIssueStates.Add(new UserIssueState
        {
            UserId = user.Id, IssueId = issue.Id,
            IsBookmarked = true, BookmarkedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var logger = StubActivityLogger();
        var service = Create(ctx2, logger: logger);
        await service.ToggleBookmarkAsync(issue.Id, user.Id);

        await logger.DidNotReceive().LogActivityAsync(Arg.Any<int>(), IssueActivityAction.Bookmarked, Arg.Any<Guid?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, string>?>());
    }

    // ── IsBookmarked ──────────────────────────────────────────────────────────

    [Fact]
    public async Task IsBookmarked_True_WhenBookmarked()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        ctx.UserIssueStates.Add(new UserIssueState
        {
            UserId = user.Id, IssueId = issue.Id, IsBookmarked = true
        });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        (await Create(ctx2).IsBookmarkedAsync(issue.Id, user.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task IsBookmarked_False_WhenNotBookmarked()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        ctx.UserIssueStates.Add(new UserIssueState
        {
            UserId = user.Id, IssueId = issue.Id, IsBookmarked = false
        });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        (await Create(ctx2).IsBookmarkedAsync(issue.Id, user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task IsBookmarked_False_WhenNoState()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        (await Create(ctx).IsBookmarkedAsync(issue.Id, user.Id)).Should().BeFalse();
    }

    // ── GetBookmarkedIssues ────────────────────────────────────────────────────

    [Fact]
    public async Task GetBookmarkedIssues_ReturnsBookmarkedOnly()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var bookmarked = await TestHelper.CreateIssueAsync(ctx, project.Id, "Bookmarked");
        var notBookmarked = await TestHelper.CreateIssueAsync(ctx, project.Id, "Not Bookmarked");
        ctx.UserIssueStates.Add(new UserIssueState { UserId = user.Id, IssueId = bookmarked.Id, IsBookmarked = true });
        ctx.UserIssueStates.Add(new UserIssueState { UserId = user.Id, IssueId = notBookmarked.Id, IsBookmarked = false });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetBookmarkedIssuesAsync(user.Id);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(bookmarked.Id);
    }

    [Fact]
    public async Task GetBookmarkedIssues_SortedByLastSeenDesc()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var now = DateTime.UtcNow;

        var issue1 = await TestHelper.CreateIssueAsync(ctx, project.Id, "Older");
        issue1.LastSeen = now.AddDays(-2);
        var issue2 = await TestHelper.CreateIssueAsync(ctx, project.Id, "Newer");
        issue2.LastSeen = now.AddDays(-1);
        await ctx.SaveChangesAsync();

        ctx.UserIssueStates.Add(new UserIssueState { UserId = user.Id, IssueId = issue1.Id, IsBookmarked = true });
        ctx.UserIssueStates.Add(new UserIssueState { UserId = user.Id, IssueId = issue2.Id, IsBookmarked = true });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2).GetBookmarkedIssuesAsync(user.Id);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(issue2.Id, "newer issue should be first");
    }

    // ── RecordIssueView ───────────────────────────────────────────────────────

    [Fact]
    public async Task RecordIssueView_CreatesStateWhenNoneExists()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var fixedTime = new DateTime(2025, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        var service = Create(ctx, StubDateTime(fixedTime));

        await service.RecordIssueViewAsync(issue.Id, user.Id);

        await using var verify = Ctx();
        var state = await verify.UserIssueStates.FindAsync(user.Id, issue.Id);
        state.Should().NotBeNull();
        state!.LastViewedAt.Should().Be(fixedTime);
    }

    [Fact]
    public async Task RecordIssueView_UpdatesExistingState()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var user = await TestHelper.CreateUserAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var oldTime = new DateTime(2025, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.UserIssueStates.Add(new UserIssueState
        {
            UserId = user.Id, IssueId = issue.Id, LastViewedAt = oldTime
        });
        await ctx.SaveChangesAsync();

        var newTime = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        await using var ctx2 = Ctx();
        var service = Create(ctx2, StubDateTime(newTime));
        await service.RecordIssueViewAsync(issue.Id, user.Id);

        await using var verify = Ctx();
        var state = await verify.UserIssueStates.FindAsync(user.Id, issue.Id);
        state!.LastViewedAt.Should().Be(newTime);
    }
}
