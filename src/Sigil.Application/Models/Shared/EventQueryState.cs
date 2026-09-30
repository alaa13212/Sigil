using System.Collections.Specialized;
using System.Web;
using Sigil.Application.Models.Events;
using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Shared;

/// <summary>Writes an event listing's filters and page into a page query string.</summary>
public static class EventQueryState
{
    public const string PageKey = "page";
    public const string LevelKey = "level";
    public const string SinceKey = "since";
    public const string UntilKey = "until";
    public const string SearchKey = "q";
    public const string SortKey = "sort";
    public const string ReleaseKey = "release";
    public const string EventIdKey = "eventId";

    /// <summary>
    /// Returns <paramref name="uri"/> with the filters and page of <paramref name="query"/> in its
    /// query string. Parameters the event listing does not own are kept, and a selection equal to
    /// the default is left out of the URL.
    /// </summary>
    public static Uri Apply(Uri uri, EventQueryParams query)
    {
        var builder = new UriBuilder(uri);
        var qs = HttpUtility.ParseQueryString(builder.Query);

        Set(qs, PageKey, query.Page.ToString());
        Set(qs, LevelKey, query.Level?.ToString());
        Set(qs, SinceKey, query.Since is { } since ? UtcDateRangeInput.ToQueryValue(since) : null);
        Set(qs, UntilKey, query.Until is { } until ? UtcDateRangeInput.ToQueryValue(until) : null);
        Set(qs, SearchKey, string.IsNullOrWhiteSpace(query.Search) ? null : query.Search);
        Set(qs, SortKey, query.SortBy == EventSortBy.Timestamp && query.SortDescending
            ? null
            : query.SortBy + (query.SortDescending ? ":desc" : ":asc"));
        Set(qs, ReleaseKey, query.ReleaseId?.ToString());
        Set(qs, EventIdKey, string.IsNullOrWhiteSpace(query.EventId) ? null : query.EventId);

        var rendered = qs.ToString() ?? string.Empty;
        builder.Query = rendered.Length == 0 ? string.Empty : "?" + rendered;
        return builder.Uri;
    }

    /// <summary>Parses a <c>sort</c> query value of the form <c>Name</c>, <c>Name:asc</c> or <c>Name:desc</c>.</summary>
    public static (EventSortBy SortBy, bool Descending)? ParseSort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var parts = value.Split(':', 2);
        if (!Enum.TryParse<EventSortBy>(parts[0], out var sortBy)) return null;
        if (parts.Length == 1) return (sortBy, true);

        var descending = parts[1].Trim();
        return (sortBy, !descending.Equals("asc", StringComparison.OrdinalIgnoreCase));
    }

    private static void Set(NameValueCollection query, string key, string? value)
    {
        if (value is null) query.Remove(key);
        else query[key] = value;
    }
}
