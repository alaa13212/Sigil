using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Events;

public record EventSummary(
    long Id,
    string? EventId,
    string? Message,
    Severity Level,
    DateTime Timestamp,
    string? Release,
    string? UserIdentifier);

public record EventDetailResponse(
    long Id,
    string? EventId,
    int IssueId,
    string? Message,
    string? ExceptionType,
    string? Culprit,
    Severity Level,
    DateTime Timestamp,
    Platform Platform,
    string? Release,
    string? Environment,
    EventUserResponse? User,
    List<StackFrameResponse> StackFrames,
    List<TagSummary> Tags,
    Dictionary<string, string>? Extra);

public record IssueEventDetailResponse(
    EventDetailResponse Event,
    List<BreadcrumbResponse> Breadcrumbs,
    EventNavigationResponse Navigation);

public record StackFrameResponse(
    string? Function,
    string? Filename,
    int? LineNumber,
    int? ColumnNumber,
    string? Module,
    bool InApp);

public record EventUserResponse(
    string? Username,
    string? Email,
    string? IpAddress,
    string? Identifier);

public record BreadcrumbResponse(
    DateTime? Timestamp,
    string? Category,
    string? Message,
    string? Level,
    string? Type,
    Dictionary<string, object>? Data);

public record EventNavigationResponse(long? PreviousEventId, long? NextEventId);
