using Sigil.Application.Models.Issues;

namespace Sigil.Application.Interfaces;

public interface IBookmarkService
{
    Task<bool> ToggleBookmarkAsync(int issueId, Guid userId);
    Task<bool> IsBookmarkedAsync(int issueId, Guid userId);
    Task<List<IssueSummary>> GetBookmarkedIssuesAsync(Guid userId);
    Task RecordIssueViewAsync(int issueId, Guid userId);
}
