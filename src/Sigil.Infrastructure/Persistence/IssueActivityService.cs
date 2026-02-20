using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Issues;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Persistence;

internal class IssueActivityService(SigilDbContext dbContext, IDateTime dateTime) : IIssueActivityService
{
    public async Task<(List<IssueActivity> Items, int TotalCount)> GetActivitiesForIssueAsync(int issueId, int page = 1, int pageSize = 50)
    {
        var query = dbContext.IssueActivities.Where(a => a.IssueId == issueId);

        int totalCount = await query.CountAsync();

        var items = await query
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<IssueActivity> LogActivityAsync(int issueId, Guid userId, IssueActivityAction action, string? message = null)
    {
        var activity = new IssueActivity
        {
            IssueId = issueId,
            UserId = userId,
            Action = action,
            Message = message,
            Timestamp = dateTime.UtcNow,
        };

        dbContext.IssueActivities.Add(activity);
        await dbContext.SaveChangesAsync();
        return activity;
    }

    // Not used server-side — the controller handles auth and calls LogActivityAsync directly
    public Task<ActivityResponse> AddCommentAsync(int issueId, string message) =>
        throw new NotSupportedException("Call LogActivityAsync with IssueActivityAction.Commented instead.");

    public async Task<PagedResponse<ActivityResponse>> GetActivitySummariesAsync(int issueId, int page = 1, int pageSize = 50)
    {
        var (items, totalCount) = await GetActivitiesForIssueAsync(issueId, page, pageSize);

        var summaries = items.Select(a => new ActivityResponse(
            a.Id, a.Action, a.Message, a.Timestamp,
            a.User?.DisplayName, a.UserId)).ToList();

        return new PagedResponse<ActivityResponse>(summaries, totalCount, page, pageSize);
    }
}
