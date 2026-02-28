using System.Collections;
using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Events;

public record EventSummary(
    long Id,
    string? EventId,
    string? Message,
    Severity Level,
    DateTime Timestamp,
    string? Release);

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
    Dictionary<string, string>? Extra,
    List<ExceptionResponse>? Exceptions = null);

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
    string? Identifier,
    Dictionary<string, string>? Extra);

public record BreadcrumbResponse(
    DateTime? Timestamp,
    string? Category,
    string? Message,
    string? Level,
    string? Type,
    Dictionary<string, object>? Data);

public record ExceptionResponse(
    string? Type,
    string? Value,
    string? Module,
    bool IsPrimary,
    bool IsSynthetic,
    List<StackFrameResponse> StackFrames);

public record EventNavigationResponse(long? PreviousEventId, long? NextEventId);
