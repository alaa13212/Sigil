using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Reingestion;

public record ReingestionJobResponse(
    int Id,
    int ProjectId,
    int? IssueId,
    ReingestionJobStatus Status,
    int TotalEvents,
    int ProcessedEvents,
    int MovedEvents,
    int DeletedEvents,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string? Error,
    string? CreatedByName);
