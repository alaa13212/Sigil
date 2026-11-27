using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:int}/limits")]
public class ProjectLimitsController(IProjectLimitsService projectLimitsService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet]
    public async Task<IActionResult> Get(int projectId) =>
        Ok(await projectLimitsService.GetAsync(projectId));
}
