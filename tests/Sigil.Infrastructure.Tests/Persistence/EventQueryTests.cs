using Sigil.Application.Interfaces;
using Sigil.Application.Models.Events;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class EventQueryTests(TestDatabaseFixture fixture)
{
    private static readonly DateTime Anchor = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static EventService Create(SigilDbContext ctx)
        => new(ctx,
            Substitute.For<ICompressionService>(),
            Substitute.For<IDateTime>(),
            Substitute.For<ISourceMapService>());

    private static DateTimeOffset Utc(DateTime value) => new(value, TimeSpan.Zero);

    // ── Since / Until ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Since_ExcludesEventsBeforeTheBound()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddHours(-2));
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor);
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { Since = Utc(Anchor) });

        result.Items.Should().ContainSingle().Which.Timestamp.Should().Be(Anchor);
    }

    [Fact]
    public async Task Until_ExcludesEventsAfterTheBound()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddHours(2));
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { Until = Utc(Anchor) });

        result.Items.Should().ContainSingle().Which.Timestamp.Should().Be(Anchor);
    }

    [Fact]
    public async Task SinceAndUntil_AreInclusiveOnBothBounds()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor);
        var service = Create(ctx);

        var exact = await service.SearchEventsAsync(project.Id,
            new EventQueryParams { Since = Utc(Anchor), Until = Utc(Anchor) });

        exact.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task SinceAfterUntil_Throws()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx);

        var act = () => service.SearchEventsAsync(project.Id, new EventQueryParams
        {
            Since = Utc(Anchor),
            Until = Utc(Anchor.AddDays(-1)),
        });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*earlier than Until*");
    }

    [Fact]
    public async Task RangeWiderThanTheCap_Throws()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx);

        var act = () => service.SearchEventsAsync(project.Id, new EventQueryParams
        {
            Since = Utc(Anchor),
            Until = Utc(Anchor.AddDays(366)),
        });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cannot span more than 365 days*");
    }

    [Fact]
    public async Task RangeAtTheCap_IsAccepted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor);
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams
        {
            Since = Utc(Anchor),
            Until = Utc(Anchor.AddDays(365)),
        });

        result.Items.Should().ContainSingle();
    }

    // ── Level, release, logger, platform, user ───────────────────────────────

    [Fact]
    public async Task Level_FiltersToASingleSeverity()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, level: Severity.Error);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1), level: Severity.Warning);
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { Level = Severity.Error });

        result.Items.Should().ContainSingle().Which.Level.Should().Be(Severity.Error);
    }

    [Fact]
    public async Task Release_FiltersToEventsOnThatRelease()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"r-{Guid.NewGuid():N}");
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, releaseId: release.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1));
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { ReleaseId = release.Id });

        result.Items.Should().ContainSingle().Which.Release.Should().Be(release.RawName);
    }

    [Fact]
    public async Task Logger_MatchesTheLoggerName()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, logger: "Billing.Payments");
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1), logger: "Billing.Invoices");
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { Logger = "Billing.Payments" });

        result.Items.Should().ContainSingle().Which.Logger.Should().Be("Billing.Payments");
    }

    [Fact]
    public async Task Platform_FiltersToASingleSdkPlatform()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, platform: Platform.Java);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1), platform: Platform.CSharp);
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { Platform = Platform.Java });

        result.Items.Should().ContainSingle().Which.Platform.Should().Be(Platform.Java);
    }

    [Fact]
    public async Task UserId_FiltersToEventsForThatUser()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var eventUser = await TestHelper.CreateEventUserAsync(ctx);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, userId: eventUser.UniqueIdentifier);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1));
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { UserId = eventUser.UniqueIdentifier });

        result.Items.Should().ContainSingle();
        result.TotalCount.Should().Be(1);
    }

    // ── Search ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_MatchesTheMessage()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, message: "Gateway timeout after 30s");
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1), message: "Null reference in worker");
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { Search = "timeout" });

        result.Items.Should().ContainSingle().Which.Message.Should().Be("Gateway timeout after 30s");
    }

    [Fact]
    public async Task Search_MatchesTheExceptionType()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, exceptionType: "System.TimeoutException");
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1), exceptionType: "System.NullReferenceException");
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { Search = "TimeoutException" });

        result.Items.Should().ContainSingle().Which.ExceptionType.Should().Be("System.TimeoutException");
    }

    [Fact]
    public async Task Search_MatchesTheCulprit()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, culprit: "CheckoutController.Pay");
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1), culprit: "CartController.Add");
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { Search = "CheckoutController" });

        result.Items.Should().ContainSingle().Which.Culprit.Should().Be("CheckoutController.Pay");
    }

    [Fact]
    public async Task Search_MultipleWords_AreAnded()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, message: "Database connection timeout");
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1), message: "Database deadlock");
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { Search = "database timeout" });

        result.Items.Should().ContainSingle().Which.Message.Should().Be("Database connection timeout");
    }

    [Fact]
    public async Task Search_TagFilter_IsAppliedFromTheSharedGrammar()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var tagged = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1));
        await TestHelper.TagEventAsync(ctx, tagged.Id, "tag", "env:prod");
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { Search = "tag:env:prod" });

        result.Items.Should().ContainSingle().Which.Id.Should().Be(tagged.Id);
    }

    [Fact]
    public async Task Search_TagFilterAndFreeText_AreBothApplied()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var matching = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, message: "Database timeout");
        await TestHelper.TagEventAsync(ctx, matching.Id, "level", "error");
        var wrongText = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1), message: "Database deadlock");
        await TestHelper.TagEventAsync(ctx, wrongText.Id, "level", "error");
        var wrongTag = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(2), message: "Database timeout");
        await TestHelper.TagEventAsync(ctx, wrongTag.Id, "level", "warning");
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { Search = "level:error timeout" });

        result.Items.Should().ContainSingle().Which.Id.Should().Be(matching.Id);
    }

    // ── EventId ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task EventId_ResolvesTheSingleExactEvent()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var target = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1));
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams { EventId = target.EventId });

        result.Items.Should().ContainSingle().Which.Id.Should().Be(target.Id);
    }

    [Fact]
    public async Task EventId_TakesPrecedenceOverTheFreeTextTerm()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var target = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, message: "Unrelated");
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams
        {
            EventId = target.EventId,
            Search = "something-that-does-not-appear-anywhere",
        });

        result.Items.Should().ContainSingle().Which.Id.Should().Be(target.Id);
    }

    [Fact]
    public async Task EventId_WithoutAFreeTextTerm_StillFiltersOutOtherEvents()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var target = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1));
        var service = Create(ctx);

        var alone = await service.SearchEventsAsync(project.Id, new EventQueryParams { EventId = target.EventId });
        var freeTextOnly = await service.SearchEventsAsync(project.Id, new EventQueryParams { Search = target.EventId });

        alone.Items.Should().ContainSingle();
        freeTextOnly.Items.Should().BeEmpty();
    }

    // ── Sorting ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SortBy_DefaultsToTimestampDescending()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddHours(1));
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddHours(2));
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams());

        result.Items.Select(e => e.Timestamp).Should().ContainInOrder(
            Anchor.AddHours(2), Anchor.AddHours(1), Anchor);
    }

    [Fact]
    public async Task SortBy_TimestampAscending_ReversesTheDefault()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddHours(1));
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id,
            new EventQueryParams { SortBy = EventSortBy.Timestamp, SortDescending = false });

        result.Items.Select(e => e.Timestamp).Should().ContainInOrder(Anchor, Anchor.AddHours(1));
    }

    [Fact]
    public async Task SortBy_Level_OrdersBySeverity()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor, level: Severity.Error);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddHours(1), level: Severity.Debug);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddHours(2), level: Severity.Fatal);
        var service = Create(ctx);

        var descending = await service.SearchEventsAsync(project.Id,
            new EventQueryParams { SortBy = EventSortBy.Level });
        var ascending = await service.SearchEventsAsync(project.Id,
            new EventQueryParams { SortBy = EventSortBy.Level, SortDescending = false });

        descending.Items.Select(e => e.Level).Should().ContainInOrder(Severity.Fatal, Severity.Error, Severity.Debug);
        ascending.Items.Select(e => e.Level).Should().ContainInOrder(Severity.Debug, Severity.Error, Severity.Fatal);
    }

    [Fact]
    public async Task SortBy_EventId_OrdersByTheSdkEventId()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var a = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor);
        var b = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddHours(1));
        var service = Create(ctx);

        var ascending = await service.SearchEventsAsync(project.Id,
            new EventQueryParams { SortBy = EventSortBy.EventId, SortDescending = false });
        var descending = await service.SearchEventsAsync(project.Id,
            new EventQueryParams { SortBy = EventSortBy.EventId });

        var expected = string.CompareOrdinal(a.EventId, b.EventId) <= 0
            ? new[] { a.EventId, b.EventId }
            : new[] { b.EventId, a.EventId };

        ascending.Items.Select(e => e.EventId).Should().ContainInOrder(expected);
        descending.Items.Select(e => e.EventId).Should().ContainInOrder(expected.Reverse());
    }

    // ── Scoping and paging ───────────────────────────────────────────────────

    [Fact]
    public async Task SearchEvents_SpansEveryIssueInTheProject()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var first = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var second = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var a = await TestHelper.CreateEventAsync(ctx, project.Id, first.Id, timestamp: Anchor);
        var b = await TestHelper.CreateEventAsync(ctx, project.Id, second.Id, timestamp: Anchor.AddHours(1));
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams());

        result.TotalCount.Should().Be(2);
        result.Items.Select(e => e.Id).Should().BeEquivalentTo(new[] { a.Id, b.Id });
        result.Items.Select(e => e.IssueId).Should().BeEquivalentTo(new[] { first.Id, second.Id });
    }

    [Fact]
    public async Task SearchEvents_ExcludesOtherProjects()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var other = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var otherIssue = await TestHelper.CreateIssueAsync(ctx, other.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor);
        await TestHelper.CreateEventAsync(ctx, other.Id, otherIssue.Id, timestamp: Anchor);
        var service = Create(ctx);

        var result = await service.SearchEventsAsync(project.Id, new EventQueryParams());

        result.Items.Should().ContainSingle();
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Paging_TotalCountCoversTheWholeFilteredSet()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        for (int i = 0; i < 7; i++)
            await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(i));
        var service = Create(ctx);

        var page1 = await service.SearchEventsAsync(project.Id, new EventQueryParams { Page = 1, PageSize = 3 });
        var page2 = await service.SearchEventsAsync(project.Id, new EventQueryParams { Page = 2, PageSize = 3 });
        var page3 = await service.SearchEventsAsync(project.Id, new EventQueryParams { Page = 3, PageSize = 3 });

        page1.Items.Should().HaveCount(3);
        page2.Items.Should().HaveCount(3);
        page3.Items.Should().HaveCount(1);
        page1.TotalCount.Should().Be(7);
        page3.TotalCount.Should().Be(7);
        page1.Page.Should().Be(1);
        page2.PageSize.Should().Be(3);
        page1.TotalPages.Should().Be(3);
        (page1.Items.Concat(page2.Items).Concat(page3.Items).Select(e => e.Id))
            .Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetEventSummaries_FiltersAndEnrichesTheIssueEventList()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, $"s-{Guid.NewGuid():N}");
        var match = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor,
            level: Severity.Fatal, message: "Disk full", exceptionType: "IOException",
            culprit: "Storage.Flush", logger: "Storage.Writer", platform: Platform.Go, releaseId: release.Id);
        await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id, timestamp: Anchor.AddMinutes(1), level: Severity.Debug);
        var service = Create(ctx);

        var result = await service.GetEventSummariesAsync(issue.Id,
            new EventQueryParams { Level = Severity.Fatal, ReleaseId = release.Id });

        result.TotalCount.Should().Be(1);
        var summary = result.Items.Should().ContainSingle().Subject;
        summary.Id.Should().Be(match.Id);
        summary.Release.Should().Be(release.RawName);
        summary.ExceptionType.Should().Be("IOException");
        summary.Culprit.Should().Be("Storage.Flush");
        summary.Logger.Should().Be("Storage.Writer");
        summary.Platform.Should().Be(Platform.Go);
        summary.IssueId.Should().Be(issue.Id);
    }
}
