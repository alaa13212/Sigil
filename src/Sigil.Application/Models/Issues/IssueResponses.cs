using Sigil.Application.Models.Events;
using Sigil.Application.Models.MergeSets;
using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Issues;

public record IssueSummary(
    int Id,
    string Title,
    string? ExceptionType,
    string? Culprit,
    IssueStatus Status,
    Priority Priority,
    Severity Level,
    DateTime FirstSeen,
    DateTime LastSeen,
    int OccurrenceCount,
    string? AssignedToName,
    int? MergeSetId = null,
    int MergeSetSize = 0);

public record IssueDetailResponse(
    int Id,
    int ProjectId,
    string Title,
    string? ExceptionType,
    string? Culprit,
    string Fingerprint,
    IssueStatus Status,
    Priority Priority,
    Severity Level,
    DateTime FirstSeen,
    DateTime LastSeen,
    int OccurrenceCount,
    string? AssignedToName,
    Guid? AssignedToId,
    string? ResolvedByName,
    DateTime? ResolvedAt,
    List<IssueTagGroup> Tags,
    EventSummary? SuggestedEvent,
    string? FirstRelease,
    string? LastRelease,
    MergeSetResponse? MergeSet = null);

public record IssueTagGroup(string Key, List<IssueTagValue> Values, int TotalCount);

public record IssueTagValue(string Value, int Count);

public record UpdateStatusRequest(IssueStatus Status, bool IgnoreFutureEvents = false);

public record AssignRequest(Guid? UserId);

public record UpdatePriorityRequest(Priority Priority);
public record AddCommentRequest(string Message);
