using Sigil.Domain.Enums;

namespace Sigil.Application.Models.FailedEvents;

public record FailedEventSummary(
    long Id,
    int ProjectId,
    string ErrorMessage,
    string? ExceptionType,
    FailedEventStage Stage,
    DateTime CreatedAt,
    bool Reprocessed,
    DateTime? ReprocessedAt);
