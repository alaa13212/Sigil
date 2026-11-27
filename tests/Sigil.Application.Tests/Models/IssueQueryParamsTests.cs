using System.Globalization;
using Sigil.Application.Models;
using Sigil.Application.Models.Shared;

namespace Sigil.Application.Tests.Models;

public class IssueQueryParamsTests
{
    [Fact]
    public void Default_SortDescendingIsTrue()
    {
        // SortDescending defaults to true — changing it silently reverses API sort order
        new IssueQueryParams().SortDescending.Should().BeTrue();
    }

    [Fact]
    public void Default_SinceAndUntilAreNull()
    {
        // Null bounds mean the issue list is not filtered by time at all
        var query = new IssueQueryParams();

        query.Since.Should().BeNull();
        query.Until.Should().BeNull();
    }

    [Fact]
    public void Defaults_OtherThanDateRangeAreUnchanged()
    {
        var query = new IssueQueryParams();

        query.Status.Should().BeNull();
        query.Priority.Should().BeNull();
        query.Level.Should().BeNull();
        query.Search.Should().BeNull();
        query.AssignedToId.Should().BeNull();
        query.SortBy.Should().Be(Sigil.Domain.Enums.IssueSortBy.LastSeen);
        query.Page.Should().Be(1);
        query.PageSize.Should().Be(50);
        query.BookmarkedByUserId.Should().BeNull();
        query.Bookmarked.Should().BeFalse();
        query.ViewerUserId.Should().BeNull();
    }
}

public class UtcDateRangeInputTests
{
    [Fact]
    public void StartOfUtcDay_FromPlusThreeBrowser_IsNotShifted()
    {
        // A browser at +03:00 sends "2026-09-01". The range must start at 2026-09-01T00:00:00Z,
        // not at 2026-09-01T00:00:00+03:00, which would move the window three hours earlier.
        UtcDateRangeInput.TryParseDate("2026-09-01", out var date).Should().BeTrue();

        var start = UtcDateRangeInput.StartOfUtcDay(date);

        start.Should().Be(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        start.UtcDateTime.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        start.Should().NotBe(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.FromHours(3)));
        start.ToOffset(TimeSpan.FromHours(3)).Should()
            .Be(new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.FromHours(3)));
    }

    [Fact]
    public void EndOfUtcDay_FromPlusThreeBrowser_CoversWholeUtcDay()
    {
        UtcDateRangeInput.TryParseDate("2026-09-07", out var date).Should().BeTrue();

        var end = UtcDateRangeInput.EndOfUtcDay(date);

        end.Should().Be(new DateTimeOffset(2026, 9, 7, 23, 59, 59, 999, TimeSpan.Zero).AddTicks(9999));
        end.UtcDateTime.Should().Be(new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1));
        end.Should().NotBe(new DateTimeOffset(2026, 9, 7, 23, 59, 59, 999, TimeSpan.FromHours(3)).AddTicks(9999));
    }

    [Fact]
    public void CustomDateRange_RoundTripsToTheSameInstants()
    {
        UtcDateRangeInput.TryParseDate("2026-02-28", out var from).Should().BeTrue();
        UtcDateRangeInput.TryParseDate("2026-03-01", out var to).Should().BeTrue();

        var range = new DateRange(UtcDateRangeInput.StartOfUtcDay(from), UtcDateRangeInput.EndOfUtcDay(to));

        UtcDateRangeInput.TryParseDate(UtcDateRangeInput.ToDateString(range.Since!.Value), out var fromAgain).Should().BeTrue();
        UtcDateRangeInput.TryParseDate(UtcDateRangeInput.ToDateString(range.Until!.Value), out var toAgain).Should().BeTrue();
        UtcDateRangeInput.StartOfUtcDay(fromAgain).Should().Be(range.Since);
        UtcDateRangeInput.EndOfUtcDay(toAgain).Should().Be(range.Until);
    }

    [Fact]
    public void QueryValue_RoundTripsAsUtcInstant()
    {
        var instant = new DateTimeOffset(2026, 9, 1, 6, 30, 0, TimeSpan.FromHours(3));

        var queryValue = UtcDateRangeInput.ToQueryValue(instant);

        UtcDateRangeInput.TryParseQueryValue(queryValue, out var parsed).Should().BeTrue();
        parsed.Should().Be(instant);
        parsed.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Parsing_UnderPersianCalendar_UsesGregorianDates()
    {
        // fa-IR formats and parses with the Persian calendar, which would otherwise shift or
        // reinterpret calendar dates. Invariant parsing must stay Gregorian.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fa-IR");

            UtcDateRangeInput.TryParseDate("2026-09-01", out var date).Should().BeTrue();

            date.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
            UtcDateRangeInput.ToDateString(UtcDateRangeInput.StartOfUtcDay(date)).Should().Be("2026-09-01");
            UtcDateRangeInput.ToQueryValue(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero))
                .Should().Be("2026-09-01T00:00:00.0000000Z");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("2026-9-1")]
    [InlineData("01/09/2026")]
    [InlineData("2026-09-01T00:00:00Z")]
    [InlineData("۱۴۰۵/۰۶/۱۰")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseDate_RejectsNonExactValues(string? value)
    {
        UtcDateRangeInput.TryParseDate(value, out _).Should().BeFalse();
    }

    [Fact]
    public void IsEndOfUtcDay_MatchesWholeDayBoundaries()
    {
        UtcDateRangeInput.TryParseDate("2026-09-07", out var date).Should().BeTrue();

        UtcDateRangeInput.IsEndOfUtcDay(UtcDateRangeInput.EndOfUtcDay(date)).Should().BeTrue();
        UtcDateRangeInput.IsEndOfUtcDay(UtcDateRangeInput.StartOfUtcDay(date)).Should().BeFalse();
        UtcDateRangeInput.IsEndOfUtcDay(DateTimeOffset.UtcNow).Should().BeFalse();
    }
}
