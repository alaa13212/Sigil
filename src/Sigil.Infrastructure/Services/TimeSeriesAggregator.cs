using System.Globalization;
using Sigil.Application.Models.Common;

namespace Sigil.Infrastructure.Services;

/// <summary>Buckets timestamped counts into aligned, gap-free series windows.</summary>
internal static class TimeSeriesAggregator
{
    private static readonly TimeSpan SixHours = TimeSpan.FromHours(6);
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
    private static readonly TimeSpan OneWeek = TimeSpan.FromDays(7);
    private static readonly TimeSpan SevenDays = TimeSpan.FromDays(7);
    private static readonly TimeSpan NinetyDays = TimeSpan.FromDays(90);

    /// <summary>Resolves a requested granularity against a window length, applying the <c>Auto</c> rules.</summary>
    public static TimeSeriesGranularity ResolveGranularity(TimeSeriesGranularity requested, TimeSpan range)
    {
        if (requested != TimeSeriesGranularity.Auto) return requested;

        if (range <= OneDay) return TimeSeriesGranularity.Hour;
        if (range <= SevenDays) return TimeSeriesGranularity.SixHour;
        if (range <= NinetyDays) return TimeSeriesGranularity.Day;
        return TimeSeriesGranularity.Week;
    }

    /// <summary>Bucket width of a resolved granularity.</summary>
    public static TimeSpan IntervalFor(TimeSeriesGranularity granularity) => granularity switch
    {
        TimeSeriesGranularity.Hour => TimeSpan.FromHours(1),
        TimeSeriesGranularity.SixHour => SixHours,
        TimeSeriesGranularity.Day => OneDay,
        TimeSeriesGranularity.Week => OneWeek,
        _ => throw new ArgumentOutOfRangeException(
            nameof(granularity), granularity, "Auto must be resolved before an interval is requested."),
    };

    /// <summary>Returns the UTC start of the bucket containing <paramref name="instant"/>.</summary>
    public static DateTimeOffset AlignTo(DateTimeOffset instant, TimeSeriesGranularity granularity)
        => new(Floor(instant.UtcDateTime, granularity), TimeSpan.Zero);

    /// <summary>
    /// Buckets <paramref name="points"/> over <c>[from, to)</c> and emits every bucket in the window,
    /// including buckets that received no counts.
    /// </summary>
    public static TimeSeries Build(
        IEnumerable<TimeSeriesPoint> points,
        DateTimeOffset from,
        DateTimeOffset to,
        TimeSeriesGranularity granularity = TimeSeriesGranularity.Auto)
    {
        var resolved = ResolveGranularity(granularity, to - from);
        var interval = IntervalFor(resolved);
        var first = AlignTo(from, resolved);

        var counts = new Dictionary<DateTimeOffset, int>();
        foreach (var point in points)
        {
            var bucket = AlignTo(point.Start, resolved);
            if (bucket < first || bucket >= to) continue;
            counts[bucket] = counts.GetValueOrDefault(bucket) + point.Count;
        }

        var buckets = new List<TimeSeriesPoint>();
        for (var cursor = first; cursor < to; cursor += interval)
            buckets.Add(new TimeSeriesPoint(cursor, counts.GetValueOrDefault(cursor)));

        return new TimeSeries(first, to, interval, buckets);
    }

    /// <summary>Truncates a UTC instant to the start of its bucket.</summary>
    private static DateTime Floor(DateTime utc, TimeSeriesGranularity granularity) => granularity switch
    {
        TimeSeriesGranularity.Hour =>
            new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc),
        TimeSeriesGranularity.SixHour =>
            new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour - utc.Hour % 6, 0, 0, DateTimeKind.Utc),
        TimeSeriesGranularity.Day =>
            new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc),
        TimeSeriesGranularity.Week => IsoWeekStart(utc),
        _ => throw new ArgumentOutOfRangeException(
            nameof(granularity), granularity, "Auto must be resolved before an instant can be aligned."),
    };

    /// <summary>Monday 00:00 UTC of the ISO week containing <paramref name="utc"/>.</summary>
    private static DateTime IsoWeekStart(DateTime utc)
    {
        var date = new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc);
        var isoYear = ISOWeek.GetYear(date);
        var isoWeek = ISOWeek.GetWeekOfYear(date);
        return DateTime.SpecifyKind(ISOWeek.ToDateTime(isoYear, isoWeek, DayOfWeek.Monday), DateTimeKind.Utc);
    }
}
