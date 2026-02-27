using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Search;

public record SearchResultsResponse(
    List<IssueSearchResult> Issues,
    List<ReleaseSearchResult> Releases,
    List<TagSearchResult> Tags);

public record IssueSearchResult(
    int Id,
    int ProjectId,
    string? Title,
    string? ExceptionType,
    IssueStatus Status,
    Severity Level);

public record ReleaseSearchResult(
    int Id,
    int ProjectId,
    string RawName);

public record TagSearchResult(
    string Key,
    string Value,
    int ProjectId,
    string ProjectName,
    int IssueCount);
