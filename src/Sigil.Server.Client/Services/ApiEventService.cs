using System.Net.Http.Json;
using System.Web;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Application.Models.Shared;
using Sigil.Domain.Entities;

namespace Sigil.Server.Client.Services;

public class ApiEventService(HttpClient http) : IEventService
{
    public async Task<PagedResponse<EventSummary>> GetEventSummariesAsync(int issueId, EventQueryParams query)
    {
        return await http.GetFromJsonAsync<PagedResponse<EventSummary>>(
            $"api/issues/{issueId}/events?{BuildQueryString(query)}")
            ?? new PagedResponse<EventSummary>([], 0, query.Page, query.PageSize);
    }

    public async Task<PagedResponse<EventSummary>> SearchEventsAsync(int projectId, EventQueryParams query)
    {
        return await http.GetFromJsonAsync<PagedResponse<EventSummary>>(
            $"api/projects/{projectId}/events?{BuildQueryString(query)}")
            ?? new PagedResponse<EventSummary>([], 0, query.Page, query.PageSize);
    }

    private static string BuildQueryString(EventQueryParams query)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        if (query.Since.HasValue) qs["since"] = UtcDateRangeInput.ToQueryValue(query.Since.Value);
        if (query.Until.HasValue) qs["until"] = UtcDateRangeInput.ToQueryValue(query.Until.Value);
        if (query.Level.HasValue) qs["level"] = query.Level.Value.ToString();
        if (query.ReleaseId.HasValue) qs["releaseId"] = query.ReleaseId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(query.Logger)) qs["logger"] = query.Logger.Trim();
        if (query.Platform.HasValue) qs["platform"] = query.Platform.Value.ToString();
        if (!string.IsNullOrWhiteSpace(query.UserId)) qs["userId"] = query.UserId.Trim();
        if (!string.IsNullOrWhiteSpace(query.Search)) qs["search"] = query.Search;
        if (!string.IsNullOrWhiteSpace(query.EventId)) qs["eventId"] = query.EventId.Trim();
        qs["sortBy"] = query.SortBy.ToString();
        qs["sortDesc"] = query.SortDescending ? "true" : "false";
        qs["page"] = query.Page.ToString();
        qs["pageSize"] = query.PageSize.ToString();
        return qs.ToString() ?? string.Empty;
    }

    public async Task<EventDetailResponse?> GetEventDetailAsync(long eventId)
    {
        try
        {
            return await http.GetFromJsonAsync<EventDetailResponse>($"api/events/{eventId}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<List<BreadcrumbResponse>> GetBreadcrumbsAsync(long eventId)
    {
        try
        {
            return await http.GetFromJsonAsync<List<BreadcrumbResponse>>($"api/events/{eventId}/breadcrumbs") ?? [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    public async Task<EventNavigationResponse> GetAdjacentEventIdsAsync(int issueId, long currentEventId)
    {
        try
        {
            return await http.GetFromJsonAsync<EventNavigationResponse>(
                $"api/issues/{issueId}/events/{currentEventId}/adjacent") ?? new EventNavigationResponse(null, null);
        }
        catch (HttpRequestException)
        {
            return new EventNavigationResponse(null, null);
        }
    }

    public async Task<IssueEventDetailResponse?> GetIssueEventDetailAsync(int issueId, long eventId)
    {
        try
        {
            return await http.GetFromJsonAsync<IssueEventDetailResponse>($"api/issues/{issueId}/events/{eventId}/detail");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<(List<CapturedEvent> Items, int TotalCount)> GetEventsForIssueAsync(int issueId, EventQueryParams query)
    {
        var response = await GetEventSummariesAsync(issueId, query);
        var events = response.Items.Select(s => new CapturedEvent
        {
            Id = s.Id, EventId = s.EventId ?? "", Message = s.Message,
            Level = s.Level, Timestamp = s.Timestamp, ReceivedAt = s.Timestamp,
            IssueId = s.IssueId ?? issueId, Platform = s.Platform, RawCompressedJson = null,
        }).ToList();
        return (events, response.TotalCount);
    }

    public async Task<CapturedEvent?> GetEventByIdAsync(long eventId, bool includeStackFrames = false, bool includeTags = false)
    {
        var detail = await GetEventDetailAsync(eventId);
        if (detail is null) return null;

        var evt = new CapturedEvent
        {
            Id = detail.Id, EventId = detail.EventId ?? "", IssueId = detail.IssueId,
            Message = detail.Message, Level = detail.Level, Timestamp = detail.Timestamp,
            Platform = detail.Platform, ReceivedAt = detail.Timestamp, RawCompressedJson = null,
        };

        if (detail.StackFrames.Count > 0)
        {
            evt.StackFrames = detail.StackFrames.Select(f => new StackFrame
            {
                Function = f.Function, Filename = f.Filename, LineNumber = f.LineNumber,
                ColumnNumber = f.ColumnNumber, Module = f.Module, InApp = f.InApp,
            }).ToList();
        }

        if (detail.User is not null)
        {
            evt.User = new EventUser
            {
                UniqueIdentifier = "", Username = detail.User.Username,
                Email = detail.User.Email, IpAddress = detail.User.IpAddress,
                Identifier = detail.User.Identifier,
            };
        }

        return evt;
    }

    public async Task<byte[]?> GetRawEventJsonAsync(long eventId)
    {
        try
        {
            return await http.GetByteArrayAsync($"events/{eventId}/raw");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
    
    
    public async Task<string?> GetEventMarkdownAsync(long eventId)
    {
        try
        {
            return await http.GetStringAsync($"events/{eventId}/md");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<EventNavigationResponse> GetMergeGroupEventNavigationAsync(int mergeSetId, long currentEventId)
    {
        try
        {
            return await http.GetFromJsonAsync<EventNavigationResponse>(
                $"api/mergesets/{mergeSetId}/events/{currentEventId}/adjacent") ?? new EventNavigationResponse(null, null);
        }
        catch (HttpRequestException)
        {
            return new EventNavigationResponse(null, null);
        }
    }

}
