using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceCode;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:int}/repositories")]
public class ProjectRepositoriesController(ISourceCodeService sourceCodeService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet]
    public async Task<IActionResult> List(int projectId) =>
        Ok(await sourceCodeService.GetRepositoriesAsync(projectId));

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost]
    public async Task<IActionResult> Link(int projectId, [FromBody] LinkRepositoryRequest request)
    {
        var repo = await sourceCodeService.LinkRepositoryAsync(projectId, request);
        return CreatedAtAction(nameof(List), new { projectId }, repo);
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpDelete("{repositoryId:int}")]
    public async Task<IActionResult> Unlink(int projectId, int repositoryId)
    {
        var deleted = await sourceCodeService.UnlinkRepositoryAsync(projectId, repositoryId);
        return deleted ? NoContent() : NotFound();
    }
}
