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

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPut("api/projects/{projectId:int}/filters/{id:int}")]
    public async Task<IActionResult> Update(int projectId, int id, [FromBody] UpdateFilterRequest request)
    {
        var filter = await filterService.UpdateFilterAsync(projectId, id, request);
        return filter is not null ? Ok(filter) : NotFound();
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpDelete("api/projects/{projectId:int}/filters/{id:int}")]
    public async Task<IActionResult> Delete(int projectId, int id)
    {
        var deleted = await filterService.DeleteFilterAsync(projectId, id);
        return deleted ? NoContent() : NotFound();
    }
}
