using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:int}/cors-origins")]
public class ProjectCorsController(IProjectCorsService projectCorsService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet]
    public async Task<IActionResult> Get(int projectId) =>
        Ok(new ProjectCorsOriginsResponse(await projectCorsService.GetOriginsAsync(projectId)));

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPut]
    public async Task<IActionResult> Set(int projectId, [FromBody] SetCorsOriginsRequest request)
    {
        try
        {
            var origins = await projectCorsService.SetOriginsAsync(projectId, request);
            return Ok(new ProjectCorsOriginsResponse(origins));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
