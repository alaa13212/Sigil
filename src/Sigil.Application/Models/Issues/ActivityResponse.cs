using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Issues;

public record ActivityResponse(
    int Id,
    IssueActivityAction Action,
    string? Message,
    DateTime Timestamp,
    string? UserName,
    Guid? UserId);

/// <param name="IssueId">Issue the activity belongs to.</param>
/// <param name="Action">What happened.</param>
public record IssueActivityEntry(int IssueId, IssueActivityAction Action);
