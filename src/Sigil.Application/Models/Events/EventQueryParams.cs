using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Events;

/// <summary>
/// Filters and paging for event listings. Since and Until are inclusive bounds on
/// <c>CapturedEvent.Timestamp</c>, expressed as UTC instants; either bound may be omitted to
/// leave that end open. Search is free text matched against the message, exception type and
/// culprit, optionally mixed with <c>key:value</c> tag filters. EventId restricts the result to
/// the single event carrying that SDK event id and takes precedence over the free-text part of
/// Search.
/// </summary>
public record EventQueryParams
{
    /// <summary>Inclusive lower bound on <c>CapturedEvent.Timestamp</c>, as a UTC instant.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>Inclusive upper bound on <c>CapturedEvent.Timestamp</c>, as a UTC instant.</summary>
    public DateTimeOffset? Until { get; init; }

    /// <summary>Restricts the result to a single severity level.</summary>
    public Severity? Level { get; init; }

    /// <summary>Restricts the result to events linked to this release.</summary>
    public int? ReleaseId { get; init; }

    /// <summary>Restricts the result to events reported by this logger.</summary>
    public string? Logger { get; init; }

    /// <summary>Restricts the result to events from this SDK platform.</summary>
    public Platform? Platform { get; init; }

    /// <summary>Restricts the result to events attributed to this user unique identifier.</summary>
    public string? UserId { get; init; }

    /// <summary>Free text and <c>key:value</c> tag filters, parsed by <c>IssueSearchParser</c>.</summary>
    public string? Search { get; init; }

    /// <summary>Exact SDK event id to resolve.</summary>
    public string? EventId { get; init; }

    public EventSortBy SortBy { get; init; } = EventSortBy.Timestamp;
    public bool SortDescending { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
