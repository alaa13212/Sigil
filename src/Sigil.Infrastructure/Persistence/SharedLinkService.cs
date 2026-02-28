using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Application.Models.Shared;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class SharedLinkService(SigilDbContext dbContext, IIssueService issueService, IEventService eventService) : ISharedLinkService
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaxDuration = TimeSpan.FromDays(7);

    public async Task<SharedIssueLinkResponse> CreateLinkAsync(int issueId, Guid userId, string hostUrl, TimeSpan? duration = null)
    {
        var ttl = duration.HasValue
            ? TimeSpan.FromTicks(Math.Min(duration.Value.Ticks, MaxDuration.Ticks))
            : DefaultDuration;

        var link = new SharedIssueLink
        {
            Token = Guid.NewGuid(),
            IssueId = issueId,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow + ttl
        };

        dbContext.SharedIssueLinks.Add(link);
        await dbContext.SaveChangesAsync();

        var url = $"{hostUrl.TrimEnd('/')}/shared/{link.Token}";
        return new SharedIssueLinkResponse(link.Token, link.IssueId, link.CreatedAt, link.ExpiresAt, url);
    }

    public async Task<SharedIssueViewResponse?> ValidateLinkAsync(Guid token)
    {
        var link = await ValidateTokenAsync(token);
        if (link is null) return null;

        var issue = await issueService.GetIssueDetailAsync(link.IssueId);
        if (issue is null) return null;

        IssueEventDetailResponse? initialEvent = null;
        if (issue.SuggestedEvent is not null)
            initialEvent = await eventService.GetIssueEventDetailAsync(link.IssueId, issue.SuggestedEvent.Id);

        return new SharedIssueViewResponse(issue, link.ExpiresAt, initialEvent);
    }

    public async Task<PagedResponse<EventSummary>?> GetSharedEventsAsync(Guid token, int page, int pageSize)
    {
        var link = await ValidateTokenAsync(token);
        if (link is null) return null;
        return await eventService.GetEventSummariesAsync(link.IssueId, page, pageSize);
    }

    public async Task<IssueEventDetailResponse?> GetSharedEventDetailAsync(Guid token, long eventId)
    {
        var link = await ValidateTokenAsync(token);
        if (link is null) return null;
        return await eventService.GetIssueEventDetailAsync(link.IssueId, eventId);
    }

    public async Task<bool> RevokeLinkAsync(Guid token)
    {
        return await dbContext.SharedIssueLinks
            .Where(l => l.Token == token)
            .ExecuteDeleteAsync() > 0;
    }

    private async Task<SharedIssueLink?> ValidateTokenAsync(Guid token)
    {
        var link = await dbContext.SharedIssueLinks
            .FirstOrDefaultAsync(l => l.Token == token);
        return link is null || link.ExpiresAt < DateTime.UtcNow ? null : link;
    }
}
