using Sigil.Application.Models.Shared;

namespace Sigil.Server.Client.Services;

/// <summary>Parses query-string values that [SupplyParameterFromQuery] cannot bind directly.</summary>
public static class QueryValue
{
    /// <summary>Parses an enum member name, ignoring case; an unknown or undefined value yields null.</summary>
    public static TEnum? Enum<TEnum>(string? value) where TEnum : struct, System.Enum =>
        System.Enum.TryParse(value, ignoreCase: true, out TEnum parsed) && System.Enum.IsDefined(parsed)
            ? parsed
            : null;

    /// <summary>Parses a UTC instant written by <see cref="Format"/>; an unparsable value yields null.</summary>
    public static DateTimeOffset? Instant(string? value) =>
        UtcDateRangeInput.TryParseQueryValue(value, out DateTimeOffset parsed) ? parsed : null;

    /// <summary>Formats an instant as a query-string value; a missing instant yields null.</summary>
    public static string? Format(DateTimeOffset? value) =>
        value is { } instant ? UtcDateRangeInput.ToQueryValue(instant) : null;
}
