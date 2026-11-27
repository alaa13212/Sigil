using System.Globalization;

namespace Sigil.Application.Models.Shared;

/// <summary>
/// Converts calendar dates, <c>&lt;input type="date"&gt;</c> values and query-string timestamps
/// into UTC instants. A calendar date is a UTC calendar date: it is never read as browser-local
/// midnight, so the same input always yields the same instant. All parsing and formatting uses the
/// invariant culture and therefore the Gregorian calendar.
/// </summary>
public static class UtcDateRangeInput
{
    /// <summary>Wire format for a calendar date, as used by <c>&lt;input type="date"&gt;</c>.</summary>
    public const string DateFormat = "yyyy-MM-dd";

    private const string TimestampFormat = "O";

    private static readonly TimeSpan EndOfDayTimeOfDay = TimeSpan.FromDays(1) - TimeSpan.FromTicks(1);

    /// <summary>
    /// Parses a <c>yyyy-MM-dd</c> calendar date into a UTC <see cref="DateTime"/>.
    /// </summary>
    public static bool TryParseDate(string? value, out DateTime date) =>
        DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);

    /// <summary>First instant of the UTC calendar day containing <paramref name="date"/>.</summary>
    public static DateTimeOffset StartOfUtcDay(DateTime date) =>
        new(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc), TimeSpan.Zero);

    /// <summary>Last instant of the UTC calendar day containing <paramref name="date"/>.</summary>
    public static DateTimeOffset EndOfUtcDay(DateTime date) =>
        new(StartOfUtcDay(date).UtcDateTime.AddDays(1).AddTicks(-1), TimeSpan.Zero);

    /// <summary>True when the instant is the last tick of a UTC calendar day.</summary>
    public static bool IsEndOfUtcDay(DateTimeOffset instant) => instant.UtcDateTime.TimeOfDay == EndOfDayTimeOfDay;

    /// <summary>Formats the UTC calendar date of an instant for a date input.</summary>
    public static string ToDateString(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>Formats an instant as a round-trippable UTC timestamp for a query string.</summary>
    public static string ToQueryValue(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    /// <summary>Parses a query-string UTC timestamp written by <see cref="ToQueryValue"/>.</summary>
    public static bool TryParseQueryValue(string? value, out DateTimeOffset instant) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out instant);
}
