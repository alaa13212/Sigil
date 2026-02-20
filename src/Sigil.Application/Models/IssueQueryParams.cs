using Sigil.Domain.Enums;

namespace Sigil.Application.Models;

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

    // Client-side only: when true, server uses the current user's ID as BookmarkedByUserId
    public bool Bookmarked { get; init; }
}
