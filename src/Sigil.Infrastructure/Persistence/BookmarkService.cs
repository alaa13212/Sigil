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
        var state = await dbContext.UserIssueStates.AsTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IssueId == issueId);

        if (state is not null)
        {
            state.IsBookmarked = !state.IsBookmarked;
            state.BookmarkedAt = state.IsBookmarked ? dateTime.UtcNow : null;
        }
        else
        {
            state = new UserIssueState
            {
                UserId = userId,
                IssueId = issueId,
                IsBookmarked = true,
                BookmarkedAt = dateTime.UtcNow
            };
            dbContext.UserIssueStates.Add(state);
        }

        await dbContext.SaveChangesAsync();

        if (state.IsBookmarked)
            await activityService.LogActivityAsync(issueId, IssueActivityAction.Bookmarked, userId);

        return state.IsBookmarked;
    }

    public async Task<bool> IsBookmarkedAsync(int issueId, Guid userId)
    {
        return await dbContext.UserIssueStates
            .AnyAsync(s => s.UserId == userId && s.IssueId == issueId && s.IsBookmarked);
    }

    public async Task<List<IssueSummary>> GetBookmarkedIssuesAsync(Guid userId)
    {
        var bookmarkedIssues = await dbContext.UserIssueStates
            .Where(s => s.UserId == userId && s.IsBookmarked)
            .Include(s => s.Issue!.AssignedTo)
            .Include(s => s.Issue!.MergeSet)
            .OrderByDescending(s => s.Issue!.LastSeen)
            .Select(s => s.Issue!)
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

    public async Task RecordIssueViewAsync(int issueId, Guid userId)
    {
        var state = await dbContext.UserIssueStates.AsTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IssueId == issueId);

        if (state is not null)
        {
            state.LastViewedAt = dateTime.UtcNow;
        }
        else
        {
            dbContext.UserIssueStates.Add(new UserIssueState
            {
                UserId = userId,
                IssueId = issueId,
                LastViewedAt = dateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync();
    }
}
