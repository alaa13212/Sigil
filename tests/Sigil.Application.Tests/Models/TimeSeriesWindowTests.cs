using Sigil.Application.Models.Common;

namespace Sigil.Application.Tests.Models;

public class TimeSeriesWindowTests
{
    private static readonly DateTime Now = new(2026, 3, 15, 13, 45, 0, DateTimeKind.Utc);

    [Fact]
    public void CalendarDays_OneDay_IsTheCurrentCalendarDay()
    {
        var (from, to) = TimeSeriesWindow.CalendarDays(Now, 1);

        from.Should().Be(new DateTimeOffset(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero));
        to.Should().Be(new DateTimeOffset(new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero));
    }

    [Fact]
    public void CalendarDays_SevenDays_StartsSevenDaysBeforeToday()
    {
        var (from, to) = TimeSeriesWindow.CalendarDays(Now, 7);

        from.Should().Be(new DateTimeOffset(new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero));
        to.Should().Be(new DateTimeOffset(new DateTime(2026, 3, 16, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero));
    }

    [Fact]
    public void Trailing_EndsAtNow()
    {
        var (from, to) = TimeSeriesWindow.Trailing(Now, 7);

        to.Should().Be(new DateTimeOffset(Now, TimeSpan.Zero));
        from.Should().Be(new DateTimeOffset(Now.AddDays(-7), TimeSpan.Zero));
    }

    [Fact]
    public void Trailing_OneDay_SpansExactlyTwentyFourHours()
    {
        var (from, to) = TimeSeriesWindow.Trailing(Now, 1);

        (to - from).Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void Trailing_FollowsTheEndInstant()
    {
        var morning = new DateTime(2026, 3, 15, 1, 0, 0, DateTimeKind.Utc);
        var evening = new DateTime(2026, 3, 15, 23, 0, 0, DateTimeKind.Utc);

        TimeSeriesWindow.Trailing(morning, 1).From.Should().Be(new DateTimeOffset(morning.AddDays(-1), TimeSpan.Zero));
        TimeSeriesWindow.Trailing(evening, 1).From.Should().Be(new DateTimeOffset(evening.AddDays(-1), TimeSpan.Zero));
    }

    [Fact]
    public void CalendarDays_IgnoresTheTimeOfDay()
    {
        var morning = new DateTime(2026, 3, 15, 1, 0, 0, DateTimeKind.Utc);
        var evening = new DateTime(2026, 3, 15, 23, 0, 0, DateTimeKind.Utc);

        TimeSeriesWindow.CalendarDays(morning, 1).From.Should().Be(TimeSeriesWindow.CalendarDays(evening, 1).From);
    }
}
