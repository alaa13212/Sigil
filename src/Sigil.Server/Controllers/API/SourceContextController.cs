using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class SourceContextController(ISourceCodeService sourceCodeService) : SigilController
{
    /// <summary>
    /// Fetches source context for a stack frame within an event.
    /// The server resolves the project and commit SHA from the event's release automatically.
    /// </summary>
    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("api/events/{eventId:long}/source-context")]
    public async Task<IActionResult> GetSourceContext(
        long eventId,
        [FromQuery] string filename,
        [FromQuery] int line)
    {
        var result = await sourceCodeService.GetSourceContextForEventAsync(eventId, filename, line);
        return result is not null ? Ok(result) : NotFound();
    }

    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("api/projects/{projectId:int}/commits/{commitSha}")]
    public async Task<IActionResult> GetCommitInfo(int projectId, string commitSha)
    {
        var result = await sourceCodeService.GetCommitInfoAsync(projectId, commitSha);
        return result is not null ? Ok(result) : NotFound();
    }
}
