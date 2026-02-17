using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Events;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Server.Client.Services;

public class ApiEventService(HttpClient http) : IEventService
{
    public async Task<PagedResponse<EventSummary>> GetEventSummariesAsync(int issueId, int page = 1, int pageSize = 50)
    {
        return await http.GetFromJsonAsync<PagedResponse<EventSummary>>(
            $"api/issues/{issueId}/events?page={page}&pageSize={pageSize}")
            ?? new PagedResponse<EventSummary>([], 0, page, pageSize);
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

    public async Task<(List<CapturedEvent> Items, int TotalCount)> GetEventsForIssueAsync(int issueId, int page = 1, int pageSize = 50)
    {
        var response = await GetEventSummariesAsync(issueId, page, pageSize);
        var events = response.Items.Select(s => new CapturedEvent
        {
            Id = s.Id, EventId = s.EventId ?? "", Message = s.Message,
            Level = s.Level, Timestamp = s.Timestamp, ReceivedAt = s.Timestamp, RawCompressedJson = null,
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

    public Task<HashSet<string>> FindExistingEventIdsAsync(IEnumerable<string> eventIds) =>
        throw new NotSupportedException("Not available on client.");

    public IEnumerable<CapturedEvent> BulkCreateEventsEntities(IEnumerable<ParsedEvent> capturedEvent, Project project,
        Issue issue, Dictionary<string, Release> releases, Dictionary<string, EventUser> users,
        Dictionary<string, TagValue> tagValues) =>
        throw new NotSupportedException("Not available on client.");

    public Task<bool> SaveEventsAsync(IEnumerable<CapturedEvent> capturedEvents) =>
        throw new NotSupportedException("Not available on client.");
}
