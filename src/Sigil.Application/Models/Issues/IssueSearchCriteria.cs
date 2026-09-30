namespace Sigil.Application.Models.Issues;

/// <summary>Assignment state selected by an <c>is:</c> search token.</summary>
public enum IssueAssignmentFilter
{
    Any,
    Assigned,
    Unassigned,
}

/// <summary>The parts of an issue search query, with the reserved tokens resolved into their own fields.</summary>
/// <param name="FreeText">Words with no <c>key:value</c> form, matched against the issue full-text vector.</param>
/// <param name="TagFilters">Tag filters to match, all of which an issue must satisfy.</param>
/// <param name="Releases">Release names to match; an issue must have an event in every one of them.</param>
/// <param name="Assignment">Assignment state to match.</param>
/// <param name="Bookmarked">True when the query carries <c>is:bookmarked</c>.</param>
/// <param name="Unviewed">True when the query carries <c>is:unviewed</c>.</param>
public sealed record IssueSearchCriteria(
    string? FreeText,
    IReadOnlyList<(string Key, string Value)> TagFilters,
    IReadOnlyList<string> Releases,
    IssueAssignmentFilter Assignment,
    bool Bookmarked,
    bool Unviewed)
{
    /// <summary>Criteria for a query with no tokens at all.</summary>
    public static readonly IssueSearchCriteria Empty =
        new(null, [], [], IssueAssignmentFilter.Any, false, false);
}
