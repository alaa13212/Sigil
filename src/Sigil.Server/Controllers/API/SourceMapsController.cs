using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:int}/releases/{releaseName}/sourcemaps")]
public class SourceMapsController(ISourceMapService sourceMapService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost]
    [RequestSizeLimit(55 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        int projectId,
        string releaseName,
        IFormFile file,
        [FromForm] string minifiedFilePath)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (string.IsNullOrWhiteSpace(minifiedFilePath))
            return BadRequest(new { error = "minifiedFilePath is required." });

        var decodedReleaseName = Uri.UnescapeDataString(releaseName);
        try
        {
            await using var stream = file.OpenReadStream();
            var result = await sourceMapService.UploadAsync(projectId, decodedReleaseName, minifiedFilePath, stream);
            return StatusCode(201, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet]
    public async Task<IActionResult> List(int projectId, string releaseName)
    {
        var decoded = Uri.UnescapeDataString(releaseName);
        return Ok(await sourceMapService.ListByReleaseNameAsync(projectId, decoded));
    }
}

[ApiController]
[Authorize]
[Route("api/projects/{projectId:int}/source-maps")]
public class SourceMapsByIdController(ISourceMapService sourceMapService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int projectId, int id)
    {
        var deleted = await sourceMapService.DeleteAsync(projectId, id);
        return deleted ? NoContent() : NotFound();
    }
}
