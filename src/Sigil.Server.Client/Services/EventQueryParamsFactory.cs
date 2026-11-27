using Sigil.Application.Models.Events;
using Sigil.Application.Models.Shared;
using Sigil.Domain.Enums;

namespace Sigil.Server.Client.Services;

/// <summary>Builds event listing filters from the values a page read out of the query string.</summary>
public static class EventQueryParamsFactory
{
    /// <summary>
    /// Builds the listing filters. A missing or malformed value falls back to the default: the
    /// newest events first, on the first page, without a severity, release or date bound.
    /// </summary>
    public static EventQueryParams Create(
        int page = 1,
        string? search = null,
        string? sort = null,
        string? level = null,
        int? releaseId = null,
        string? eventId = null,
        string? since = null,
        string? until = null,
        int pageSize = 50)
    {
        var selection = EventQueryState.ParseSort(sort);

        return new EventQueryParams
        {
            Since = QueryValue.Instant(since),
            Until = QueryValue.Instant(until),
            Level = QueryValue.Enum<Severity>(level),
            ReleaseId = releaseId,
            Search = search,
            EventId = eventId,
            SortBy = selection is { } parsed ? parsed.SortBy : EventSortBy.Timestamp,
            SortDescending = selection is { } choice ? choice.Descending : true,
            Page = Math.Max(1, page),
            PageSize = pageSize,
        };
    }
}
