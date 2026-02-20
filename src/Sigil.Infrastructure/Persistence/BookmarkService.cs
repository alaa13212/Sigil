using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Issues;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Persistence;

internal class BookmarkService(SigilDbContext dbContext, IDateTime dateTime, IIssueActivityService activityService) : IBookmarkService
{
    public async Task<bool> ToggleBookmarkAsync(int issueId, Guid userId)
    {
        var existing = await dbContext.IssueBookmarks.AsTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.IssueId == issueId);

        if (existing is not null)
        {
            dbContext.IssueBookmarks.Remove(existing);
            await dbContext.SaveChangesAsync();
            return false;
        }

        dbContext.IssueBookmarks.Add(new IssueBookmark
        {
            UserId = userId,
            IssueId = issueId,
            CreatedAt = dateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        await activityService.LogActivityAsync(issueId, userId, IssueActivityAction.Bookmarked);
        return true;
    }

    public async Task<bool> IsBookmarkedAsync(int issueId, Guid userId)
    {
        return await dbContext.IssueBookmarks
            .AnyAsync(b => b.UserId == userId && b.IssueId == issueId);
    }

    public async Task<List<IssueSummary>> GetBookmarkedIssuesAsync(Guid userId)
    {
        var bookmarkedIssues = await dbContext.IssueBookmarks
            .Where(b => b.UserId == userId)
            .Include(b => b.Issue!.AssignedTo)
            .Include(b => b.Issue!.MergeSet)
            .OrderByDescending(b => b.Issue!.LastSeen)
            .Select(b => b.Issue!)
            .ToListAsync();

        return bookmarkedIssues.Select(i => new IssueSummary(
            i.Id, i.Title, i.ExceptionType, i.Culprit,
            i.Status, i.Priority,
            i.MergeSet?.Level ?? i.Level,
            i.MergeSet?.FirstSeen ?? i.FirstSeen,
            i.MergeSet?.LastSeen ?? i.LastSeen,
            i.MergeSet?.OccurrenceCount ?? i.OccurrenceCount,
            i.AssignedTo?.DisplayName,
            i.MergeSetId)).ToList();
    }
}
