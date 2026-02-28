using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Filters;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class EventFiltersController(IEventFilterService filterService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("api/projects/{projectId:int}/filters")]
    public async Task<IActionResult> List(int projectId)
    {
        return Ok(await filterService.GetFiltersAsync(projectId));
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost("api/projects/{projectId:int}/filters")]
    public async Task<IActionResult> Create(int projectId, [FromBody] CreateFilterRequest request)
    {
        var filter = await filterService.CreateFilterAsync(projectId, request);
        return Ok(filter);
    }

    [HttpPut("api/filters/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFilterRequest request)
    {
        var filter = await filterService.UpdateFilterAsync(id, request);
        return filter is not null ? Ok(filter) : NotFound();
    }

    [HttpDelete("api/filters/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await filterService.DeleteFilterAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
