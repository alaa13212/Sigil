namespace Sigil.Application.Models.Common;

/// <summary>Windows a time series can cover.</summary>
public static class TimeSeriesWindow
{
    /// <summary>Half-open UTC window of the trailing <paramref name="days"/> calendar days, today included.</summary>
    public static (DateTimeOffset From, DateTimeOffset To) CalendarDays(DateTime utcNow, int days)
    {
        var today = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).Date;
        var from = new DateTimeOffset(today.AddDays(-days + 1), TimeSpan.Zero);
        return (from, from.AddDays(days));
    }

    /// <summary>Half-open UTC window of the trailing <paramref name="days"/> days, ending at <paramref name="utcNow"/>.</summary>
    public static (DateTimeOffset From, DateTimeOffset To) Trailing(DateTime utcNow, int days)
    {
        var to = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), TimeSpan.Zero);
        return (to.AddDays(-days), to);
    }
}
