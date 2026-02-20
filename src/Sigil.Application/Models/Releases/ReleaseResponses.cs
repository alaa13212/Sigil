using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Releases;

public record ReleaseHealthSummary(
    int Id,
    string RawName,
    string? SemanticVersion,
    DateTime FirstSeenAt,
    int TotalEvents,
    int NewIssues,
    int RegressionCount,
    int AffectedIssues,
    DateTime? LastEventAt);

public record ReleaseDetailResponse(
    int Id,
    string RawName,
    string? SemanticVersion,
    string? Package,
    string? Build,
    string? CommitSha,
    DateTime FirstSeenAt,
    int TotalEvents,
    int NewIssues,
    int RegressionCount,
    int AffectedIssues,
    List<ReleaseIssueSummary> TopIssues);

public record ReleaseIssueSummary(
    int IssueId,
    string Title,
    string? ExceptionType,
    int EventCount,
    bool IsNew,
    Severity Level);
