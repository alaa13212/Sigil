using Sigil.Domain.Enums;

namespace Sigil.Application.Models;

/// <summary>
/// Filters and paging for issue listings. Since and Until are inclusive bounds on
/// <c>Issue.LastSeen</c> — the instant an issue was last active — expressed as UTC instants;
/// either bound may be omitted to leave that end open.
/// </summary>
public record IssueQueryParams
{
    public IssueStatus? Status { get; init; }
    public Priority? Priority { get; init; }
    public Severity? Level { get; init; }
    public string? Search { get; init; }
    public Guid? AssignedToId { get; init; }
    public IssueSortBy SortBy { get; init; } = IssueSortBy.LastSeen;
    public bool SortDescending { get; init; } = true;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public Guid? BookmarkedByUserId { get; init; }

    /// <summary>Inclusive lower bound on <c>Issue.LastSeen</c>, as a UTC instant.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>Inclusive upper bound on <c>Issue.LastSeen</c>, as a UTC instant.</summary>
    public DateTimeOffset? Until { get; init; }

    // Client-side only: when true, server uses the current user's ID as BookmarkedByUserId
    public bool Bookmarked { get; init; }

    // Server-side: viewer user ID for unviewed status computation
    public Guid? ViewerUserId { get; init; }
}
