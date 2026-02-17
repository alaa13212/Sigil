using Sigil.Application.Models.Events;
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
    string? AssignedToName);

public record IssueDetailResponse(
    int Id,
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
    List<TagSummary> Tags,
    EventSummary? SuggestedEvent);

public record UpdateStatusRequest(IssueStatus Status);

public record AssignRequest(Guid? UserId);

public record UpdatePriorityRequest(Priority Priority);
