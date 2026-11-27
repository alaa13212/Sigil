using Sigil.Application.Models.Common;
using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class TimeSeriesAggregatorTests
{
    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc), TimeSpan.Zero);

    // ── Granularity selection ────────────────────────────────────────────────

    [Theory]
    [InlineData(1, TimeSeriesGranularity.Hour)]
    [InlineData(7, TimeSeriesGranularity.SixHour)]
    [InlineData(30, TimeSeriesGranularity.Day)]
    [InlineData(90, TimeSeriesGranularity.Day)]
    [InlineData(91, TimeSeriesGranularity.Week)]
    [InlineData(365, TimeSeriesGranularity.Week)]
    public void ResolveGranularity_Auto_PicksWidthForRange(int days, TimeSeriesGranularity expected)
    {
        var resolved = TimeSeriesAggregator.ResolveGranularity(
            TimeSeriesGranularity.Auto, TimeSpan.FromDays(days));

        resolved.Should().Be(expected);
    }

    [Fact]
    public void ResolveGranularity_Auto_HourUpperBoundaryIsInclusive()
    {
        TimeSeriesAggregator.ResolveGranularity(TimeSeriesGranularity.Auto, TimeSpan.FromHours(24))
            .Should().Be(TimeSeriesGranularity.Hour);
        TimeSeriesAggregator.ResolveGranularity(TimeSeriesGranularity.Auto, TimeSpan.FromHours(24) + TimeSpan.FromTicks(1))
            .Should().Be(TimeSeriesGranularity.SixHour);
    }

    [Fact]
    public void ResolveGranularity_Auto_SixHourUpperBoundaryIsInclusive()
    {
        TimeSeriesAggregator.ResolveGranularity(TimeSeriesGranularity.Auto, TimeSpan.FromDays(7))
            .Should().Be(TimeSeriesGranularity.SixHour);
        TimeSeriesAggregator.ResolveGranularity(TimeSeriesGranularity.Auto, TimeSpan.FromDays(7) + TimeSpan.FromTicks(1))
            .Should().Be(TimeSeriesGranularity.Day);
    }

    [Fact]
    public void ResolveGranularity_Auto_WeekUpperBoundaryIsInclusive()
    {
        TimeSeriesAggregator.ResolveGranularity(TimeSeriesGranularity.Auto, TimeSpan.FromDays(90))
            .Should().Be(TimeSeriesGranularity.Day);
        TimeSeriesAggregator.ResolveGranularity(TimeSeriesGranularity.Auto, TimeSpan.FromDays(90) + TimeSpan.FromTicks(1))
            .Should().Be(TimeSeriesGranularity.Week);
    }

    [Theory]
    [InlineData(TimeSeriesGranularity.Hour, 1)]
    [InlineData(TimeSeriesGranularity.SixHour, 6)]
    [InlineData(TimeSeriesGranularity.Day, 24)]
    [InlineData(TimeSeriesGranularity.Week, 168)]
    public void ResolveGranularity_Explicit_IsNotOverriddenByRange(TimeSeriesGranularity requested, int expectedHours)
    {
        var resolved = TimeSeriesAggregator.ResolveGranularity(requested, TimeSpan.FromDays(365));

        resolved.Should().Be(requested);
        TimeSeriesAggregator.IntervalFor(resolved).Should().Be(TimeSpan.FromHours(expectedHours));
    }

    [Fact]
    public void IntervalFor_Auto_Throws()
    {
        var act = () => TimeSeriesAggregator.IntervalFor(TimeSeriesGranularity.Auto);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AlignTo_Auto_Throws()
    {
        var act = () => TimeSeriesAggregator.AlignTo(Utc(2026, 1, 5), TimeSeriesGranularity.Auto);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── Hour alignment ────────────────────────────────────────────────────────

    [Fact]
    public void AlignTo_Hour_TruncatesToHourBoundaryUtc()
    {
        TimeSeriesAggregator.AlignTo(Utc(2026, 3, 14, 15, 42), TimeSeriesGranularity.Hour)
            .Should().Be(Utc(2026, 3, 14, 15));
    }

    [Fact]
    public void AlignTo_Hour_IgnoresNonUtcOffset()
    {
        var shifted = new DateTimeOffset(2026, 3, 14, 18, 42, 0, TimeSpan.FromHours(3));

        var aligned = TimeSeriesAggregator.AlignTo(shifted, TimeSeriesGranularity.Hour);

        aligned.Should().Be(Utc(2026, 3, 14, 15));
        aligned.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void AlignTo_SixHour_TruncatesToSixHourBoundary()
    {
        foreach (var hour in new[] { 0, 1, 5, 6, 7, 11, 12, 17, 18, 23 })
            TimeSeriesAggregator.AlignTo(Utc(2026, 3, 14, hour, 30), TimeSeriesGranularity.SixHour)
                .Should().Be(Utc(2026, 3, 14, hour - hour % 6));
    }

    [Fact]
    public void AlignTo_Day_TruncatesToMidnightUtc()
    {
        TimeSeriesAggregator.AlignTo(Utc(2026, 3, 14, 23, 59), TimeSeriesGranularity.Day)
            .Should().Be(Utc(2026, 3, 14));
    }

    [Fact]
    public void AlignTo_Day_AlignsToTheUtcDayNotTheOffsetDay()
    {
        // 02:30 at +03:00 is 23:30 UTC on the previous day.
        var shifted = new DateTimeOffset(2026, 3, 14, 2, 30, 0, TimeSpan.FromHours(3));

        TimeSeriesAggregator.AlignTo(shifted, TimeSeriesGranularity.Day).Should().Be(Utc(2026, 3, 13));
    }

    // ── ISO week alignment ────────────────────────────────────────────────────

    [Theory]
    [InlineData(2026, 1, 5)]   // Monday
    [InlineData(2026, 1, 11)]  // Sunday
    [InlineData(2026, 1, 12)]  // Monday
    [InlineData(2026, 2, 1)]   // Sunday
    [InlineData(2026, 2, 2)]   // Monday
    [InlineData(2026, 3, 1)]   // Sunday
    [InlineData(2026, 3, 29)]  // Sunday, month rollover
    [InlineData(2026, 3, 30)]  // Monday after month rollover
    [InlineData(2025, 12, 28)] // Sunday, year rollover
    [InlineData(2025, 12, 29)] // Monday after year rollover
    [InlineData(2026, 1, 1)]   // Thursday in the week that began 2025-12-29
    public void AlignTo_Week_IsAlwaysMondayMidnightUtc(int year, int month, int day)
    {
        var aligned = TimeSeriesAggregator.AlignTo(Utc(year, month, day, 17, 3), TimeSeriesGranularity.Week);

        aligned.DayOfWeek.Should().Be(DayOfWeek.Monday);
        aligned.Hour.Should().Be(0);
        aligned.Minute.Should().Be(0);
        aligned.Offset.Should().Be(TimeSpan.Zero);
        aligned.Should().Be(Utc(year, month, day) - TimeSpan.FromDays(((int)new DateTime(year, month, day).DayOfWeek + 6) % 7));
    }

    [Fact]
    public void AlignTo_Week_SundayBelongsToTheWeekThatStartedOnThePrecedingMonday()
    {
        // 2026-01-04 is a Sunday; 2026-01-05 is the Monday that starts the next week.
        var sunday = TimeSeriesAggregator.AlignTo(Utc(2026, 1, 4, 23, 0), TimeSeriesGranularity.Week);
        var monday = TimeSeriesAggregator.AlignTo(Utc(2026, 1, 5, 0, 0), TimeSeriesGranularity.Week);

        sunday.Should().Be(Utc(2025, 12, 29));
        monday.Should().Be(Utc(2026, 1, 5));
        sunday.Should().NotBe(monday);
    }

    [Fact]
    public void AlignTo_Week_SundayAndTheMondayOfItsOwnWeekShareAWeek()
    {
        var monday = TimeSeriesAggregator.AlignTo(Utc(2026, 1, 5, 0, 0), TimeSeriesGranularity.Week);
        var sunday = TimeSeriesAggregator.AlignTo(Utc(2026, 1, 11, 23, 59), TimeSeriesGranularity.Week);

        sunday.Should().Be(monday);
        monday.Should().Be(Utc(2026, 1, 5));
    }

    [Fact]
    public void AlignTo_Week_SundayAndTheImmediatelyFollowingMondayAreInDifferentWeeks()
    {
        // 2026-01-04 is a Sunday; the week it belongs to began on 2025-12-29.
        var sunday = TimeSeriesAggregator.AlignTo(Utc(2026, 1, 4, 20, 0), TimeSeriesGranularity.Week);
        var nextMonday = TimeSeriesAggregator.AlignTo(Utc(2026, 1, 5, 0, 0), TimeSeriesGranularity.Week);

        sunday.Should().Be(Utc(2025, 12, 29));
        nextMonday.Should().Be(Utc(2026, 1, 5));
        (nextMonday - sunday).Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void Build_Week_MidWeekStart_ProducesBucketsSpanningSevenDays()
    {
        // Wednesday 2026-01-07 through the end of February.
        var series = TimeSeriesAggregator.Build(
            [], Utc(2026, 1, 7), Utc(2026, 3, 1), TimeSeriesGranularity.Week);

        series.From.Should().Be(Utc(2026, 1, 5));
        series.Interval.Should().Be(TimeSpan.FromDays(7));
        series.Points.Should().HaveCount(8);
        series.Points.Should().OnlyContain(p => p.Start.DayOfWeek == DayOfWeek.Monday);
        for (int i = 1; i < series.Points.Count; i++)
            (series.Points[i].Start - series.Points[i - 1].Start).Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void Build_Week_EachBucketSpansSevenDaysAcrossAMonthRollover()
    {
        var series = TimeSeriesAggregator.Build(
            [], Utc(2026, 2, 1), Utc(2026, 4, 1), TimeSeriesGranularity.Week);

        series.From.Should().Be(Utc(2026, 1, 26));
        series.Points.Should().OnlyContain(p => p.Start.DayOfWeek == DayOfWeek.Monday);
        series.Points.Should().Contain(p => p.Start.Month == 2);
        series.Points.Should().Contain(p => p.Start.Month == 3);
    }

    [Fact]
    public void Build_Week_EachBucketSpansSevenDaysAcrossAYearRollover()
    {
        var series = TimeSeriesAggregator.Build(
            [], Utc(2025, 12, 1), Utc(2026, 1, 15), TimeSeriesGranularity.Week);

        series.From.Should().Be(Utc(2025, 12, 1));
        series.Points.Should().OnlyContain(p => p.Start.DayOfWeek == DayOfWeek.Monday);
        series.Points.Should().Contain(p => p.Start.Year == 2025);
        series.Points.Should().Contain(p => p.Start.Year == 2026);
        series.Points.Should().OnlyContain(p => p.Start < series.To);
        for (int i = 1; i < series.Points.Count; i++)
            (series.Points[i].Start - series.Points[i - 1].Start).Should().Be(TimeSpan.FromDays(7));
    }

    [Fact]
    public void Build_Week_SundayAndFollowingMondayLandInDifferentBuckets()
    {
        var series = TimeSeriesAggregator.Build(
            [
                new TimeSeriesPoint(Utc(2026, 1, 11, 10, 0), 3), // Sunday
                new TimeSeriesPoint(Utc(2026, 1, 12, 10, 0), 4), // Monday
            ],
            Utc(2026, 1, 5), Utc(2026, 1, 19), TimeSeriesGranularity.Week);

        var sundayBucket = series.Points.Single(p => p.Count == 3);
        var mondayBucket = series.Points.Single(p => p.Count == 4);

        sundayBucket.Start.Should().Be(Utc(2026, 1, 5));
        mondayBucket.Start.Should().Be(Utc(2026, 1, 12));
    }

    [Fact]
    public void AlignTo_Week_FridayOfJanuaryFirstBelongsToTheLastWeekOfThePreviousIsoYear()
    {
        // 2027-01-01 is a Friday, so its week began on Monday 2026-12-28 (ISO week 53 of 2026).
        var aligned = TimeSeriesAggregator.AlignTo(Utc(2027, 1, 1), TimeSeriesGranularity.Week);

        aligned.Should().Be(Utc(2026, 12, 28));
        System.Globalization.ISOWeek.GetYear(aligned.UtcDateTime).Should().Be(2026);
        System.Globalization.ISOWeek.GetWeekOfYear(aligned.UtcDateTime).Should().Be(53);
    }

    [Fact]
    public void AlignTo_Week_MondayOfJanuaryFirstIsIsoWeekOne()
    {
        // 2029-01-01 is a Monday, so it opens ISO week 1 of 2029.
        var aligned = TimeSeriesAggregator.AlignTo(Utc(2029, 1, 1, 5, 0), TimeSeriesGranularity.Week);

        aligned.Should().Be(Utc(2029, 1, 1));
        System.Globalization.ISOWeek.GetWeekOfYear(aligned.UtcDateTime).Should().Be(1);
    }

    [Fact]
    public void AlignTo_Week_AgreesWithIsoWeekForEveryDayOfAYear()
    {
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 365; i++)
        {
            var aligned = TimeSeriesAggregator.AlignTo(new DateTimeOffset(date, TimeSpan.Zero), TimeSeriesGranularity.Week);
            var expected = System.Globalization.ISOWeek.ToDateTime(
                System.Globalization.ISOWeek.GetYear(date),
                System.Globalization.ISOWeek.GetWeekOfYear(date),
                DayOfWeek.Monday);

            aligned.UtcDateTime.Should().Be(expected, "ISO weeks start on Monday");
        }
    }

    // ── Build: sub-day, zero fill, edges ──────────────────────────────────────

    [Fact]
    public void Build_OneDay_ReturnsTwentyFourHourlyPoints()
    {
        var series = TimeSeriesAggregator.Build(
            [new TimeSeriesPoint(Utc(2026, 6, 1, 13), 5)],
            Utc(2026, 6, 1), Utc(2026, 6, 2));

        series.Interval.Should().Be(TimeSpan.FromHours(1));
        series.Points.Should().HaveCount(24);
        series.Points.Single(p => p.Start == Utc(2026, 6, 1, 13)).Count.Should().Be(5);
    }

    [Fact]
    public void Build_SevenDays_UsesSixHourBuckets()
    {
        var series = TimeSeriesAggregator.Build(
            [], Utc(2026, 6, 1), Utc(2026, 6, 8));

        series.Interval.Should().Be(TimeSpan.FromHours(6));
        series.Points.Should().HaveCount(28);
    }

    [Fact]
    public void Build_EmptyInput_StillCoversTheWindow()
    {
        var series = TimeSeriesAggregator.Build([], Utc(2026, 6, 1), Utc(2026, 6, 15));

        series.Points.Should().HaveCount(14);
        series.Points.Should().AllSatisfy(p => p.Count.Should().Be(0));
    }

    [Fact]
    public void Build_AllZeroInput_YieldsAllZeroBuckets()
    {
        var series = TimeSeriesAggregator.Build(
            [new TimeSeriesPoint(Utc(2026, 6, 3), 0)],
            Utc(2026, 6, 1), Utc(2026, 6, 15));

        series.Points.Should().HaveCount(14);
        series.Points.Should().AllSatisfy(p => p.Count.Should().Be(0));
    }

    [Fact]
    public void Build_SinglePoint_YieldsOneNonZeroBucketAndZeroFilledNeighbours()
    {
        var series = TimeSeriesAggregator.Build(
            [new TimeSeriesPoint(Utc(2026, 6, 8, 5), 9)],
            Utc(2026, 6, 1), Utc(2026, 6, 15));

        series.Points.Should().HaveCount(14);
        series.Points.Count(p => p.Count > 0).Should().Be(1);
        series.Points.Single(p => p.Count > 0).Count.Should().Be(9);
    }

    [Fact]
    public void Build_SparseInput_KeepsBucketsContinuous()
    {
        var series = TimeSeriesAggregator.Build(
            [
                new TimeSeriesPoint(Utc(2026, 6, 1, 2), 3),
                new TimeSeriesPoint(Utc(2026, 6, 14, 23), 7),
            ],
            Utc(2026, 6, 1), Utc(2026, 6, 15), TimeSeriesGranularity.Day);

        series.Points.Should().HaveCount(14);
        series.Points.Select(p => p.Start).Should().BeInAscendingOrder();
        series.Points.First().Count.Should().Be(3);
        series.Points.Last().Count.Should().Be(7);
        series.Points.Skip(1).Take(12).Should().AllSatisfy(p => p.Count.Should().Be(0));
    }

    [Fact]
    public void Build_SumsPointsThatFallInTheSameBucket()
    {
        var series = TimeSeriesAggregator.Build(
            [
                new TimeSeriesPoint(Utc(2026, 6, 1, 1), 2),
                new TimeSeriesPoint(Utc(2026, 6, 1, 23), 3),
            ],
            Utc(2026, 6, 1), Utc(2026, 6, 15));

        series.Points.First().Count.Should().Be(5);
    }

    [Fact]
    public void Build_IgnoresPointsOutsideTheWindow()
    {
        var series = TimeSeriesAggregator.Build(
            [
                new TimeSeriesPoint(Utc(2026, 5, 31, 23), 10),
                new TimeSeriesPoint(Utc(2026, 6, 1), 1),
                new TimeSeriesPoint(Utc(2026, 6, 15), 20),
            ],
            Utc(2026, 6, 1), Utc(2026, 6, 15));

        series.Points.Sum(p => p.Count).Should().Be(1);
    }

    [Fact]
    public void Build_EmptyWindow_YieldsNoPoints()
    {
        var series = TimeSeriesAggregator.Build([], Utc(2026, 6, 1), Utc(2026, 6, 1));

        series.Points.Should().BeEmpty();
    }

    [Fact]
    public void Build_NonAlignedFrom_StartsAtTheAlignedBucket()
    {
        var series = TimeSeriesAggregator.Build(
            [], Utc(2026, 6, 1, 10, 37), Utc(2026, 6, 4, 10, 37), TimeSeriesGranularity.Day);

        series.From.Should().Be(Utc(2026, 6, 1));
        series.Points.Should().HaveCount(4);
    }

    [Fact]
    public void Build_Auto_IntervalMatchesTheRange()
    {
        TimeSeriesAggregator.Build([], Utc(2026, 6, 1), Utc(2026, 6, 2)).Interval
            .Should().Be(TimeSpan.FromHours(1));
        TimeSeriesAggregator.Build([], Utc(2026, 6, 1), Utc(2026, 6, 8)).Interval
            .Should().Be(TimeSpan.FromHours(6));
        TimeSeriesAggregator.Build([], Utc(2026, 6, 1), Utc(2026, 7, 1)).Interval
            .Should().Be(TimeSpan.FromDays(1));
        TimeSeriesAggregator.Build([], Utc(2026, 1, 1), Utc(2026, 12, 1)).Interval
            .Should().Be(TimeSpan.FromDays(7));
    }
}
