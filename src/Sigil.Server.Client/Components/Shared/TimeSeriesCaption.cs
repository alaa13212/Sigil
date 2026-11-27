using System.Globalization;
using Sigil.Application.Models.Common;

namespace Sigil.Server.Client.Components.Shared;

/// <summary>Renders the range and bucket width a <see cref="TimeSeries"/> covers, read from the series itself.</summary>
public static class TimeSeriesCaption
{
    private const string DayFormat = "MMM d";
    private const string DayWithYearFormat = "MMM d, yyyy";

    /// <summary>Describes the window and interval, e.g. <c>Oct 1 – Oct 7, 2026 · 6-hour buckets</c>.</summary>
    public static string Describe(TimeSeries? series)
    {
        if (series is null || series.Points.Count == 0) return "No activity";

        var from = series.From.UtcDateTime;
        var to = series.To.UtcDateTime;
        return $"{Range(from, to)} · {Interval(series.Interval)}";
    }

    private static string Range(DateTime from, DateTime to)
    {
        var lastDay = to.AddTicks(-1);

        if (from.Date == lastDay.Date)
            return from.ToString(DayWithYearFormat, CultureInfo.InvariantCulture);

        var fromText = from.Year == lastDay.Year
            ? from.ToString(DayFormat, CultureInfo.InvariantCulture)
            : from.ToString(DayWithYearFormat, CultureInfo.InvariantCulture);

        return $"{fromText} – {lastDay.ToString(DayWithYearFormat, CultureInfo.InvariantCulture)}";
    }

    private static string Interval(TimeSpan interval) => interval switch
    {
        { TotalHours: 1 } => "hourly",
        { TotalHours: 6 } => "6-hour buckets",
        { TotalDays: 1 } => "daily",
        { TotalDays: 7 } => "weekly",
        _ => $"{interval.TotalHours:0.##}-hour buckets",
    };
}
