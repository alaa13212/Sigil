using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Issues;

public record ActivityResponse(
    int Id,
    IssueActivityAction Action,
    string? Message,
    DateTime Timestamp,
    string? UserName,
    Guid? UserId);
