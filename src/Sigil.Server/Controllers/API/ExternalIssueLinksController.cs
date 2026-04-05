using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
[Route("api/issues/{issueId:int}/external-links")]
public class ExternalIssueLinksController(IIssueTrackerService issueTrackerService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet]
    public async Task<IActionResult> List(int issueId) =>
        Ok(await issueTrackerService.GetLinksAsync(issueId));

    [Authorize(Policy = SigilPermissions.CanEditIssue)]
    [HttpPost]
    public async Task<IActionResult> Create(int issueId, [FromBody] CreateExternalIssueLinkRequest request)
    {
        try
        {
            var created = await issueTrackerService.CreateExternalIssueAsync(issueId, request.TrackerConfigId);
            return StatusCode(201, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Policy = SigilPermissions.CanEditIssue)]
    [HttpDelete("{linkId:int}")]
    public async Task<IActionResult> Delete(int linkId)
    {
        var deleted = await issueTrackerService.UnlinkAsync(linkId);
        return deleted ? NoContent() : NotFound();
    }
}

[ApiController]
[Authorize]
[Route("api/external-links")]
public class ExternalLinksByIdController(IIssueTrackerService issueTrackerService) : SigilController
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await issueTrackerService.UnlinkAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}

public record CreateExternalIssueLinkRequest(int TrackerConfigId);
