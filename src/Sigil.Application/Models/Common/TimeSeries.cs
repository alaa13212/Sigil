namespace Sigil.Application.Models.Common;

/// <summary>Bucket width of a <see cref="TimeSeries"/>. <see cref="TimeSeriesGranularity.Auto"/> picks a width from the range.</summary>
public enum TimeSeriesGranularity
{
    Auto,
    Hour,
    SixHour,
    Day,
    Week,
}

/// <summary>One bucket of a <see cref="TimeSeries"/>. <paramref name="Start"/> is the UTC instant the bucket opens.</summary>
public record TimeSeriesPoint(DateTimeOffset Start, int Count);

/// <summary>
/// Counts over the half-open window <c>[From, To)</c> in fixed <paramref name="Interval"/> steps.
/// Every bucket inside the window is present, including buckets with no data.
/// </summary>
/// <param name="From">Aligned UTC instant the first bucket opens at.</param>
/// <param name="To">Exclusive UTC end of the window.</param>
public record TimeSeries(
    DateTimeOffset From,
    DateTimeOffset To,
    TimeSpan Interval,
    IReadOnlyList<TimeSeriesPoint> Points)
{
    /// <summary>Zero-length series with no points.</summary>
    public static TimeSeries Empty { get; } = new(default, default, TimeSpan.Zero, []);
}
