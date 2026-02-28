using Sigil.Application.Models.Events;
using Sigil.Application.Models.Issues;

namespace Sigil.Application.Models.Shared;

public record CreateSharedLinkRequest(TimeSpan? Duration = null);

public record SharedIssueLinkResponse(
    Guid Token,
    int IssueId,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    string Url);

public record SharedIssueViewResponse(
    IssueDetailResponse Issue,
    DateTime ExpiresAt,
    IssueEventDetailResponse? InitialEvent);
