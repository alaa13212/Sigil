using System.Globalization;
using Sigil.Application.Models.Common;

namespace Sigil.Application.Tests.Models;

public class TimeSeriesAxisTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan SixHours = TimeSpan.FromHours(6);
    private static readonly TimeSpan Day = TimeSpan.FromDays(1);
    private static readonly TimeSpan Week = TimeSpan.FromDays(7);

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new(new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc), TimeSpan.Zero);

    private static TimeSeries Series(int pointCount, int intervalHours, params int[] counts)
    {
        var interval = TimeSpan.FromHours(intervalHours);
        var start = Utc(2026, 10, 1);
        var points = Enumerable.Range(0, pointCount)
            .Select(i => new TimeSeriesPoint(start + TimeSpan.FromHours(intervalHours * i), counts.ElementAtOrDefault(i)))
            .ToList();
        return new TimeSeries(start, start + interval * pointCount, interval, points);
    }

    // ── State classification ───────────────────────────────────────────────────

    [Fact]
    public void Classify_NullSeries_IsEmpty()
    {
        TimeSeriesAxis.Classify(null).Should().Be(TimeSeriesChartState.Empty);
    }

    [Fact]
    public void Classify_NoPoints_IsEmpty()
    {
        TimeSeriesAxis.Classify(new TimeSeries(Utc(2026, 10, 1), Utc(2026, 10, 2), Day, []))
            .Should().Be(TimeSeriesChartState.Empty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Classify_OneBucket_IsSinglePointWhateverTheCount(int count)
    {
        TimeSeriesAxis.Classify(Series(1, 24, count)).Should().Be(TimeSeriesChartState.SinglePoint);
    }

    [Fact]
    public void Classify_ManyZeroBuckets_IsAllZero()
    {
        TimeSeriesAxis.Classify(Series(7, 24, 0, 0, 0, 0, 0, 0, 0)).Should().Be(TimeSeriesChartState.AllZero);
    }

    [Fact]
    public void Classify_AnyNonZeroBucket_IsValues()
    {
        TimeSeriesAxis.Classify(Series(7, 24, 0, 0, 0, 4, 0, 0, 0)).Should().Be(TimeSeriesChartState.Values);
    }

    [Fact]
    public void Classify_ZeroLengthSeries_IsEmptyNotAllZero()
    {
        // An empty window and a window of zeros are different situations, not the same one.
        TimeSeriesAxis.Classify(TimeSeries.Empty).Should().Be(TimeSeriesChartState.Empty);
    }

    // ── Bucket bounds ──────────────────────────────────────────────────────────

    [Fact]
    public void BucketEnd_IsStartPlusInterval()
    {
        TimeSeriesAxis.BucketEnd(Utc(2026, 10, 1, 6), SixHours)
            .Should().Be(Utc(2026, 10, 1, 12));
    }

    [Fact]
    public void BucketEnd_WeeklySpansSevenDays()
    {
        TimeSeriesAxis.BucketEnd(Utc(2026, 10, 5), Week).Should().Be(Utc(2026, 10, 12));
    }

    // ── Label text ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, "Oct 1, 00:00")]
    [InlineData(6, "Oct 1, 06:00")]
    [InlineData(18, "Oct 1, 18:00")]
    public void TickLabel_SubDayBucket_IncludesHourOfDay(int hour, string expected)
    {
        TimeSeriesAxis.TickLabel(Utc(2026, 10, 1, hour), SixHours).Should().Be(expected);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(168)]
    public void TickLabel_DailyOrWider_OmitsTimeOfDay(int intervalHours)
    {
        TimeSeriesAxis.TickLabel(Utc(2026, 10, 1, 6), TimeSpan.FromHours(intervalHours))
            .Should().Be("Oct 1");
    }

    [Fact]
    public void TooltipText_CarriesSpanInUtcAndExactCount()
    {
        TimeSeriesAxis.TooltipText(Utc(2026, 10, 1, 6), SixHours, 42)
            .Should().Be("Oct 1, 06:00 – Oct 1, 12:00 UTC · 42 events");
    }

    [Fact]
    public void TooltipText_SingleEventIsNotPlural()
    {
        TimeSeriesAxis.TooltipText(Utc(2026, 10, 1), Day, 1)
            .Should().Be("Oct 1 – Oct 2 UTC · 1 event");
    }

    [Fact]
    public void Labels_UnderPersianCalendarAndCommaDecimalCulture_StayGregorianAndInvariant()
    {
        // fa-IR formats with the Persian calendar, de-DE with a comma decimal separator.
        // Axis text is written into SVG attributes, where a comma would break the value.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
            TimeSeriesAxis.TickLabel(Utc(2026, 10, 1, 6), SixHours).Should().Be("Oct 1, 06:00");
            TimeSeriesAxis.TooltipText(Utc(2026, 10, 1, 6), SixHours, 42)
                .Should().Be("Oct 1, 06:00 – Oct 1, 12:00 UTC · 42 events");

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            TimeSeriesAxis.Pixels(12.5).Should().Be("12.5");
            TimeSeriesAxis.FormatCount(1234).Should().Be("1,234");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FormatCount_GroupsThousands()
    {
        TimeSeriesAxis.FormatCount(1234567).Should().Be("1,234,567");
    }

    // ── x-axis label thinning ──────────────────────────────────────────────────

    [Fact]
    public void TickIndices_NoPoints_IsEmpty()
    {
        TimeSeriesAxis.TickIndices(0, 6).Should().BeEmpty();
    }

    [Fact]
    public void TickIndices_SinglePoint_LabelsIt()
    {
        TimeSeriesAxis.TickIndices(1, 6).Should().Equal(0);
    }

    [Fact]
    public void TickIndices_FewerPointsThanTicks_LabelsEveryBucket()
    {
        TimeSeriesAxis.TickIndices(4, 6).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void TickIndices_SevenBuckets_LabelsSixEvenlySpacedBuckets()
    {
        TimeSeriesAxis.TickIndices(7, 6).Should().Equal(0, 1, 2, 4, 5, 6);
    }

    [Fact]
    public void TickIndices_LastBucketIsAlwaysLabelled()
    {
        TimeSeriesAxis.TickIndices(24, 6).Should().Equal(0, 5, 9, 14, 18, 23);
    }

    [Theory]
    [InlineData(1, 6)]
    [InlineData(2, 6)]
    [InlineData(13, 6)]
    [InlineData(24, 6)]
    [InlineData(97, 6)]
    [InlineData(168, 6)]
    [InlineData(90, 4)]
    [InlineData(90, 2)]
    [InlineData(365, 6)]
    public void TickIndices_NeverExceedsTheTickBudget(int pointCount, int maxTicks)
    {
        var ticks = TimeSeriesAxis.TickIndices(pointCount, maxTicks);

        ticks.Should().HaveCountLessThanOrEqualTo(maxTicks);
        ticks.Should().StartWith(0);
        ticks.Should().EndWith(pointCount - 1);
        ticks.Should().BeInAscendingOrder();
        ticks.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TickIndices_NeverPlacesTwoLabelsOnTheSameBucket()
    {
        var ticks = TimeSeriesAxis.TickIndices(3, 6);

        ticks.Should().Equal(0, 1, 2);
    }

    [Theory]
    [InlineData(600, 6, 6)]
    [InlineData(200, 6, 4)]
    [InlineData(60, 6, 2)]
    [InlineData(10, 6, 2)]
    [InlineData(600, 3, 6)]
    public void TickCount_ShrinksUntilLabelsCannotTouch(int axisWidth, int widestLabel, int expected)
    {
        TimeSeriesAxis.MaxTicksForWidth(axisWidth, widestLabel).Should().Be(expected);
    }

    [Fact]
    public void TickCount_NeverExceedsTheRequestedTickBudget()
    {
        TimeSeriesAxis.MaxTicksForWidth(600, 6, 3).Should().Be(3);
    }

    [Fact]
    public void TickCount_NarrowAxis_StillLabelsBothEnds()
    {
        TimeSeriesAxis.MaxTicksForWidth(20, 14).Should().Be(2);
    }

    [Fact]
    public void TickCount_SpacedTicksFitTheAxis()
    {
        // 500px of axis, 11 characters per label, six labels: every gap must clear a label.
        var axisWidth = 500;
        var labelWidth = 11 * TimeSeriesAxis.CharWidthPixels;
        var ticks = TimeSeriesAxis.TickIndices(24, TimeSeriesAxis.MaxTicksForWidth(axisWidth, 11));

        ticks.Should().HaveCount(6);
        for (var i = 1; i < ticks.Count; i++)
        {
            var gap = (ticks[i] - ticks[i - 1]) * axisWidth / 23.0;
            gap.Should().BeGreaterThanOrEqualTo(labelWidth);
        }
    }

    // ── Label alignment ────────────────────────────────────────────────────────

    [Fact]
    public void Anchor_EdgeTicksGrowInwards()
    {
        TimeSeriesAxis.Anchor(0, 10).Should().Be("start");
        TimeSeriesAxis.Anchor(9, 10).Should().Be("end");
        TimeSeriesAxis.Anchor(4, 10).Should().Be("middle");
    }

    [Fact]
    public void Anchor_SinglePointIsCentred()
    {
        TimeSeriesAxis.Anchor(0, 1).Should().Be("middle");
    }

    // ── y-axis scale ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 10)]
    [InlineData(9, 10)]
    [InlineData(10, 10)]
    [InlineData(42, 50)]
    [InlineData(100, 100)]
    [InlineData(101, 200)]
    [InlineData(2500, 5000)]
    public void AxisMax_IsTheSmallestRoundValueAtOrAboveTheData(int value, int expected)
    {
        TimeSeriesAxis.AxisMax(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(37)]
    [InlineData(999)]
    [InlineData(123456)]
    public void AxisMax_NeverUnderstatesTheTallestBucket(int value)
    {
        TimeSeriesAxis.AxisMax(value).Should().BeGreaterThanOrEqualTo(value);
    }
}
