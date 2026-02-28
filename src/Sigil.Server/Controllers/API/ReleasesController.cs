using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Domain.Enums;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class ReleasesController(IReleaseHealthService releaseHealthService, IIssueService issueService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("api/projects/{projectId:int}/releases")]
    public async Task<IActionResult> GetReleases(int projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await releaseHealthService.GetReleaseHealthAsync(projectId, page, pageSize);

        var userId = GetUserId();
        if (userId.HasValue)
            await issueService.RecordPageViewAsync(userId.Value, projectId, PageType.Releases);

        return Ok(result);
    }

    [HttpGet("api/releases/{id:int}")]
    public async Task<IActionResult> GetRelease(int id)
    {
        var result = await releaseHealthService.GetReleaseDetailAsync(id);
        return result is not null ? Ok(result) : NotFound();
    }
}
