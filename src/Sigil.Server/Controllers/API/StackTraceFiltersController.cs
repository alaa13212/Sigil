using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Filters;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class StackTraceFiltersController(IStackTraceFilterService filterService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("api/projects/{projectId:int}/stack-trace-filters")]
    public async Task<IActionResult> GetFilters(int projectId)
        => Ok(await filterService.GetFiltersAsync(projectId));

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost("api/projects/{projectId:int}/stack-trace-filters")]
    public async Task<IActionResult> CreateFilter(int projectId, [FromBody] CreateStackTraceFilterRequest request)
        => Ok(await filterService.CreateFilterAsync(projectId, request));

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPut("api/projects/{projectId:int}/stack-trace-filters/{id:int}")]
    public async Task<IActionResult> UpdateFilter(int projectId, int id, [FromBody] UpdateStackTraceFilterRequest request)
    {
        var result = await filterService.UpdateFilterAsync(projectId, id, request);
        return result is null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpDelete("api/projects/{projectId:int}/stack-trace-filters/{id:int}")]
    public async Task<IActionResult> DeleteFilter(int projectId, int id)
        => await filterService.DeleteFilterAsync(projectId, id) ? NoContent() : NotFound();
}
