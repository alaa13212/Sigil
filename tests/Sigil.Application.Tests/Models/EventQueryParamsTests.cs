using Sigil.Application.Models.Events;
using Sigil.Application.Models.Shared;
using Sigil.Domain.Enums;

namespace Sigil.Application.Tests.Models;

public class EventQueryParamsTests
{
    [Fact]
    public void Defaults_AreNewestFirstPageOneOfFifty()
    {
        var query = new EventQueryParams();

        query.SortBy.Should().Be(EventSortBy.Timestamp);
        query.SortDescending.Should().BeTrue();
        query.Page.Should().Be(1);
        query.PageSize.Should().Be(50);
    }

    [Fact]
    public void Defaults_LeaveEveryFilterUnset()
    {
        var query = new EventQueryParams();

        query.Since.Should().BeNull();
        query.Until.Should().BeNull();
        query.Level.Should().BeNull();
        query.ReleaseId.Should().BeNull();
        query.Logger.Should().BeNull();
        query.Platform.Should().BeNull();
        query.UserId.Should().BeNull();
        query.Search.Should().BeNull();
        query.EventId.Should().BeNull();
    }

    [Fact]
    public void With_CarriesEveryFilterDimension()
    {
        var since = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var until = new DateTimeOffset(2026, 1, 8, 0, 0, 0, TimeSpan.Zero);

        var query = new EventQueryParams
        {
            Since = since,
            Until = until,
            Level = Severity.Fatal,
            ReleaseId = 7,
            Logger = "Microsoft.Hosting",
            Platform = Platform.Java,
            UserId = "user-1",
            Search = "tag:env:prod",
            EventId = "abc123",
            SortBy = EventSortBy.Level,
            SortDescending = false,
            Page = 3,
            PageSize = 10,
        };

        query.Since.Should().Be(since);
        query.Until.Should().Be(until);
        query.Level.Should().Be(Severity.Fatal);
        query.ReleaseId.Should().Be(7);
        query.Logger.Should().Be("Microsoft.Hosting");
        query.Platform.Should().Be(Platform.Java);
        query.UserId.Should().Be("user-1");
        query.Search.Should().Be("tag:env:prod");
        query.EventId.Should().Be("abc123");
        query.SortBy.Should().Be(EventSortBy.Level);
        query.SortDescending.Should().BeFalse();
        query.Page.Should().Be(3);
        query.PageSize.Should().Be(10);
    }

    [Fact]
    public void With_LeavesUnmentionedFiltersAlone()
    {
        var original = new EventQueryParams { Level = Severity.Error, Page = 4 };

        var updated = original with { SortBy = EventSortBy.EventId };

        updated.Level.Should().Be(Severity.Error);
        updated.Page.Should().Be(4);
        updated.SortBy.Should().Be(EventSortBy.EventId);
    }
}

public class EventQueryStateTests
{
    private static Uri Apply(string uri, EventQueryParams query) =>
        EventQueryState.Apply(new Uri(uri), query);

    [Fact]
    public void Apply_LeavesTheDefaultSelectionOutOfTheUrl()
    {
        var result = Apply("https://sigil.test/projects/1/issues/2", new EventQueryParams());

        result.Query.Should().Be("?page=1");
    }

    [Fact]
    public void Apply_WritesEveryFilterKey()
    {
        var query = new EventQueryParams
        {
            Since = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Until = new DateTimeOffset(2026, 1, 8, 0, 0, 0, TimeSpan.Zero),
            Level = Severity.Error,
            ReleaseId = 9,
            Search = "tag:env:prod",
            EventId = "abc123",
            SortBy = EventSortBy.Level,
            SortDescending = false,
            Page = 2,
        };

        var result = Apply("https://sigil.test/projects/1/issues/2", query);

        result.Query.Should().Contain("level=Error");
        result.Query.Should().Contain("release=9");
        result.Query.Should().Contain("q=tag%3aenv%3aprod");
        result.Query.Should().Contain("eventId=abc123");
        result.Query.Should().Contain("sort=Level%3aasc");
        result.Query.Should().Contain("page=2");
        result.Query.Should().Contain("since=2026-01-01T00%3a00%3a00.0000000Z");
    }

    [Fact]
    public void Apply_KeepsParametersTheListingDoesNotOwn()
    {
        var result = Apply("https://sigil.test/projects/1/issues/2?tab=Events&event=42&range=7",
            new EventQueryParams { Level = Severity.Warning });

        result.Query.Should().Contain("tab=Events");
        result.Query.Should().Contain("event=42");
        result.Query.Should().Contain("range=7");
        result.Query.Should().Contain("level=Warning");
    }

    [Fact]
    public void Apply_ClearsAKeyWhenTheFilterIsRemoved()
    {
        var result = Apply("https://sigil.test/projects/1/events?level=Error&release=9&eventId=abc&page=4",
            new EventQueryParams());

        result.Query.Should().NotContain("level=");
        result.Query.Should().NotContain("release=");
        result.Query.Should().NotContain("eventId=");
        result.Query.Should().Contain("page=1");
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", null, false)]
    [InlineData("Timestamp", EventSortBy.Timestamp, true)]
    [InlineData("Level:desc", EventSortBy.Level, true)]
    [InlineData("Level:asc", EventSortBy.Level, false)]
    [InlineData("EventId:ASC", EventSortBy.EventId, false)]
    [InlineData("EventId:sideways", EventSortBy.EventId, true)]
    [InlineData("NotAColumn", null, false)]
    public void ParseSort_ReadsColumnAndDirection(string? value, EventSortBy? sortBy, bool descending)
    {
        var result = EventQueryState.ParseSort(value);

        if (sortBy is null)
        {
            result.Should().BeNull();
            return;
        }

        result.Should().NotBeNull();
        result!.Value.SortBy.Should().Be(sortBy.Value);
        result.Value.Descending.Should().Be(descending);
    }
}
