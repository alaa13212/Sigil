using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class FailedEventsController(
    IFailedEventService failedEventService) : SigilController
{
    [HttpGet("api/projects/{projectId:int}/failed-events")]
    public async Task<IActionResult> List(int projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return Ok(await failedEventService.GetFailedEventsAsync(projectId, page, Math.Clamp(pageSize, 1, 100)));
    }

    [HttpGet("api/failed-events/{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var failedEvent = await failedEventService.GetByIdAsync(id);
        return failedEvent is not null ? Ok(failedEvent) : NotFound();
    }

    [HttpPost("api/failed-events/{id:long}/reprocess")]
    public async Task<IActionResult> Reprocess(long id)
    {
        bool result = await failedEventService.ReprocessAsync(id);
        return result ? Ok() : NotFound();
    }

    [HttpPost("api/projects/{projectId:int}/failed-events/reprocess")]
    public async Task<IActionResult> ReprocessAll(int projectId)
    {
        int count = await failedEventService.ReprocessAllAsync(projectId);
        return Ok(new { ReprocessedCount = count });
    }

    [HttpDelete("api/failed-events/{id:long}")]
    public async Task<IActionResult> Delete(long id)
    {
        bool result = await failedEventService.DeleteAsync(id);
        return result ? NoContent() : NotFound();
    }
}
