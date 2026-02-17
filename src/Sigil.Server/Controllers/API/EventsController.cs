using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class EventsController(
    IEventService eventService) : SigilController
{
    [HttpGet("api/issues/{issueId:int}/events")]
    public async Task<IActionResult> List(int issueId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return Ok(await eventService.GetEventSummariesAsync(issueId, page, Math.Clamp(pageSize, 1, 100)));
    }

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
}
