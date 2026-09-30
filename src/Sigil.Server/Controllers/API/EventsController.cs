using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Events;
using Sigil.Domain.Enums;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class EventsController(
    IEventService eventService,
    IProjectEntityAccess projectEntityAccess) : SigilController
{
    [HttpGet("api/issues/{issueId:int}/events")]
    public async Task<IActionResult> List(
        int issueId,
        [FromQuery] DateTimeOffset? since,
        [FromQuery] DateTimeOffset? until,
        [FromQuery] Severity? level,
        [FromQuery] int? releaseId,
        [FromQuery] string? logger,
        [FromQuery] Platform? platform,
        [FromQuery] string? userId,
        [FromQuery] string? search,
        [FromQuery] string? eventId,
        [FromQuery] EventSortBy sortBy = EventSortBy.Timestamp,
        [FromQuery] bool sortDesc = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = BuildQuery(since, until, level, releaseId, logger, platform, userId, search, eventId,
            sortBy, sortDesc, page, pageSize);

        try
        {
            return Ok(await eventService.GetEventSummariesAsync(issueId, query));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("api/projects/{projectId:int}/events")]
    public async Task<IActionResult> Search(
        int projectId,
        [FromQuery] DateTimeOffset? since,
        [FromQuery] DateTimeOffset? until,
        [FromQuery] Severity? level,
        [FromQuery] int? releaseId,
        [FromQuery] string? logger,
        [FromQuery] Platform? platform,
        [FromQuery] string? userId,
        [FromQuery] string? search,
        [FromQuery] string? eventId,
        [FromQuery] EventSortBy sortBy = EventSortBy.Timestamp,
        [FromQuery] bool sortDesc = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (await projectEntityAccess.GetProjectByIdAsync(projectId) is null)
            return NotFound();

        var query = BuildQuery(since, until, level, releaseId, logger, platform, userId, search, eventId,
            sortBy, sortDesc, page, pageSize);

        try
        {
            return Ok(await eventService.SearchEventsAsync(projectId, query));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static EventQueryParams BuildQuery(
        DateTimeOffset? since, DateTimeOffset? until, Severity? level, int? releaseId, string? logger,
        Platform? platform, string? userId, string? search, string? eventId, EventSortBy sortBy,
        bool sortDesc, int page, int pageSize) => new()
        {
            Since = since,
            Until = until,
            Level = level,
            ReleaseId = releaseId,
            Logger = logger,
            Platform = platform,
            UserId = userId,
            Search = search,
            EventId = eventId,
            SortBy = sortBy,
            SortDescending = sortDesc,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
        };

    [HttpGet("api/events/{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var detail = await eventService.GetEventDetailAsync(id);
        return detail is not null ? Ok(detail) : NotFound();
    }

    [HttpGet("events/{id:long}/raw")]
    public async Task<IActionResult> Raw(long id)
    {
        var rawBytes = await eventService.GetRawEventJsonAsync(id);
        return rawBytes is not null ? File(rawBytes, "application/json") : NotFound();
    }

    [HttpGet("events/{id:long}/md")]
    public async Task<IActionResult> Markdown(long id)
    {
        string? markdownText = await eventService.GetEventMarkdownAsync(id);
        return markdownText is not null ? File(Encoding.UTF8.GetBytes(markdownText), "text/markdown") : NotFound();
    }

    [HttpGet("events/{id:long}/download")]
    public async Task<IActionResult> Download(long id)
    {
        var rawBytes = await eventService.GetRawEventJsonAsync(id);
        return rawBytes is not null ? File(rawBytes, "application/json", $"event-{id}.json") : NotFound();
    }

    [HttpGet("api/events/{id:long}/breadcrumbs")]
    public async Task<IActionResult> Breadcrumbs(long id)
    {
        return Ok(await eventService.GetBreadcrumbsAsync(id));
    }

    [HttpGet("api/issues/{issueId:int}/events/{eventId:long}/adjacent")]
    public async Task<IActionResult> Adjacent(int issueId, long eventId)
    {
        return Ok(await eventService.GetAdjacentEventIdsAsync(issueId, eventId));
    }

    [HttpGet("api/issues/{issueId:int}/events/{eventId:long}/detail")]
    public async Task<IActionResult> IssueEventDetail(int issueId, long eventId)
    {
        var detail = await eventService.GetIssueEventDetailAsync(issueId, eventId);
        return detail is not null ? Ok(detail) : NotFound();
    }
}
