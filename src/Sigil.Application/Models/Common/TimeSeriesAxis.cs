using System.Globalization;

namespace Sigil.Application.Models.Common;

/// <summary>Shape a time series chart draws its data in.</summary>
public enum TimeSeriesChartMode
{
    Bar,
    Line,
}

/// <summary>Data situation a time series chart distinguishes when rendering.</summary>
public enum TimeSeriesChartState
{
    /// <summary>No buckets to draw.</summary>
    Empty,

    /// <summary>Exactly one bucket, whatever its count.</summary>
    SinglePoint,

    /// <summary>Several buckets, every one of them zero.</summary>
    AllZero,

    /// <summary>At least one bucket with a non-zero count.</summary>
    Values,
}

/// <summary>Range, bucket width and render mode selected on a chart header.</summary>
/// <param name="RangeDays">Length of the window, in days back from now.</param>
/// <param name="Granularity">Bucket width, or <see cref="TimeSeriesGranularity.Auto"/> to derive it from the range.</param>
/// <param name="Mode">Bar or line + area.</param>
public record TimeSeriesChartView(int RangeDays, TimeSeriesGranularity Granularity, TimeSeriesChartMode Mode)
{
    /// <summary>Window a chart shows when nothing is selected.</summary>
    public const int DefaultRangeDays = 14;

    /// <summary>View a chart uses when nothing is selected.</summary>
    public static TimeSeriesChartView Default { get; } =
        new(DefaultRangeDays, TimeSeriesGranularity.Auto, TimeSeriesChartMode.Bar);
}

/// <summary>
/// Axis geometry, label text and value scaling for a <see cref="TimeSeries"/>. Instants are read as
/// UTC and all text uses the invariant culture, so labels are identical on every host calendar.
/// </summary>
public static class TimeSeriesAxis
{
    /// <summary>Target number of x-axis ticks on a roomy chart.</summary>
    public const int DesiredTicks = 6;

    /// <summary>Approximate width of one axis-label character, in pixels, at the chart font size.</summary>
    public const int CharWidthPixels = 7;

    private const string DayFormat = "MMM d";
    private const string DayTimeFormat = "MMM d, HH:mm";
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

    /// <summary>Classifies a series into the state a chart renders for it.</summary>
    public static TimeSeriesChartState Classify(TimeSeries? series)
    {
        if (series is null || series.Points.Count == 0) return TimeSeriesChartState.Empty;
        if (series.Points.Count == 1) return TimeSeriesChartState.SinglePoint;
        return series.Points.All(p => p.Count == 0) ? TimeSeriesChartState.AllZero : TimeSeriesChartState.Values;
    }

    /// <summary>Exclusive UTC end of the bucket that opens at <paramref name="start"/>.</summary>
    public static DateTimeOffset BucketEnd(DateTimeOffset start, TimeSpan interval) => start + interval;

    /// <summary>
    /// x-axis label for a bucket start, in UTC and invariant culture. Sub-day buckets include the
    /// hour of day; daily and weekly buckets do not.
    /// </summary>
    public static string TickLabel(DateTimeOffset instant, TimeSpan interval) =>
        instant.UtcDateTime.ToString(interval < OneDay ? DayTimeFormat : DayFormat, CultureInfo.InvariantCulture);

    /// <summary>Native tooltip text for one bucket: its UTC span and exact count.</summary>
    public static string TooltipText(DateTimeOffset start, TimeSpan interval, int count)
    {
        var span = $"{TickLabel(start, interval)} – {TickLabel(BucketEnd(start, interval), interval)} UTC";
        return $"{span} · {FormatCount(count)} {(count == 1 ? "event" : "events")}";
    }

    /// <summary>
    /// How many ticks fit an axis of <paramref name="axisWidth"/> pixels without the widest of
    /// <paramref name="widestLabelLength"/> characters touching. Never returns fewer than two, so
    /// the first and last bucket stay labelled.
    /// </summary>
    public static int MaxTicksForWidth(int axisWidth, int widestLabelLength, int desiredTicks = DesiredTicks)
    {
        var labelWidth = Math.Max(1, widestLabelLength) * CharWidthPixels;
        var fitting = axisWidth <= 0 ? desiredTicks : axisWidth / labelWidth;
        return Math.Clamp(fitting, 2, Math.Max(2, desiredTicks));
    }

    /// <summary>
    /// Indices of the buckets that get an x-axis label: an evenly spaced run from the first bucket to
    /// the last. Returns at most <paramref name="maxTicks"/> indices, and never fewer than two once
    /// there is more than one bucket, so the run cannot be denser than the axis was budgeted for.
    /// </summary>
    public static IReadOnlyList<int> TickIndices(int pointCount, int maxTicks)
    {
        if (pointCount <= 0) return [];
        if (pointCount == 1) return [0];

        var count = Math.Min(pointCount, Math.Max(2, maxTicks));
        var span = pointCount - 1;

        var ticks = new List<int>(count);
        for (var position = 0; position < count; position++)
            ticks.Add((int)Math.Round((double)position * span / (count - 1), MidpointRounding.AwayFromZero));

        return ticks;
    }

    /// <summary>SVG <c>text-anchor</c> for a tick, so edge labels grow inwards instead of off the chart.</summary>
    public static string Anchor(int index, int pointCount)
    {
        if (pointCount <= 1) return "middle";
        if (index <= 0) return "start";
        return index >= pointCount - 1 ? "end" : "middle";
    }

    /// <summary>Smallest round y-axis maximum that is at least <paramref name="maxValue"/> and at least 1.</summary>
    public static int AxisMax(int maxValue)
    {
        if (maxValue <= 0) return 1;

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(maxValue)));
        var normalized = maxValue / magnitude;
        var step = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        var rounded = step * magnitude;

        return (int)Math.Ceiling(maxValue / rounded) * (int)rounded;
    }

    /// <summary>Grouped count for display, e.g. <c>1,234</c>.</summary>
    public static string FormatCount(int count) => count.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>Formats a pixel coordinate for an SVG attribute, independent of the host culture.</summary>
    public static string Pixels(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
