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

    [HttpPut("api/stack-trace-filters/{id:int}")]
    public async Task<IActionResult> UpdateFilter(int id, [FromBody] UpdateStackTraceFilterRequest request)
    {
        var result = await filterService.UpdateFilterAsync(id, request);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("api/stack-trace-filters/{id:int}")]
    public async Task<IActionResult> DeleteFilter(int id)
        => await filterService.DeleteFilterAsync(id) ? Ok() : NotFound();
}
