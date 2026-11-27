using Sigil.Domain.Enums;
using Sigil.Server.Client.Services;

namespace Sigil.Server.Tests;

public class EventQueryParamsFactoryTests
{
    [Fact]
    public void Defaults_to_the_newest_events_on_the_first_page()
    {
        var query = EventQueryParamsFactory.Create();

        query.Page.Should().Be(1);
        query.PageSize.Should().Be(50);
        query.SortBy.Should().Be(EventSortBy.Timestamp);
        query.SortDescending.Should().BeTrue();
        query.Level.Should().BeNull();
        query.Since.Should().BeNull();
        query.Until.Should().BeNull();
        query.ReleaseId.Should().BeNull();
        query.Search.Should().BeNull();
        query.EventId.Should().BeNull();
    }

    [Fact]
    public void Reads_the_page_the_search_the_release_and_the_event_id()
    {
        var query = EventQueryParamsFactory.Create(page: 3, search: "boom", releaseId: 7, eventId: "abc-123");

        query.Page.Should().Be(3);
        query.Search.Should().Be("boom");
        query.ReleaseId.Should().Be(7);
        query.EventId.Should().Be("abc-123");
    }

    [Theory]
    [InlineData("Fatal", Severity.Fatal)]
    [InlineData("fatal", Severity.Fatal)]
    [InlineData("WARNING", Severity.Warning)]
    public void Reads_the_severity(string level, Severity expected)
    {
        EventQueryParamsFactory.Create(level: level).Level.Should().Be(expected);
    }

    [Fact]
    public void Ignores_a_severity_that_is_not_a_member()
    {
        EventQueryParamsFactory.Create(level: "nonsense").Level.Should().BeNull();
    }

    [Theory]
    [InlineData("Timestamp", EventSortBy.Timestamp, true)]
    [InlineData("Level", EventSortBy.Level, true)]
    [InlineData("Level:asc", EventSortBy.Level, false)]
    [InlineData("Level:desc", EventSortBy.Level, true)]
    public void Reads_the_sort_order(string sort, EventSortBy expected, bool descending)
    {
        var query = EventQueryParamsFactory.Create(sort: sort);

        query.SortBy.Should().Be(expected);
        query.SortDescending.Should().Be(descending);
    }

    [Fact]
    public void Reads_the_date_bounds()
    {
        var query = EventQueryParamsFactory.Create(
            since: "2026-03-04T05:06:07.0000000Z",
            until: "2026-03-05T05:06:07.0000000Z");

        query.Since.Should().Be(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero));
        query.Until.Should().Be(new DateTimeOffset(2026, 3, 5, 5, 6, 7, TimeSpan.Zero));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Falls_back_to_the_first_page(int page)
    {
        EventQueryParamsFactory.Create(page: page).Page.Should().Be(1);
    }
}
