using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.IssueTrackers;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:int}/issue-tracker-configs")]
public class IssueTrackerConfigsController(IIssueTrackerService issueTrackerService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet]
    public async Task<IActionResult> List(int projectId) =>
        Ok(await issueTrackerService.GetConfigsAsync(projectId));

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost]
    public async Task<IActionResult> Create(int projectId, [FromBody] CreateTrackerConfigRequest request)
    {
        var created = await issueTrackerService.AddConfigAsync(projectId, request);
        return StatusCode(201, created);
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int projectId, int id, [FromBody] UpdateTrackerConfigRequest request)
    {
        var updated = await issueTrackerService.UpdateConfigAsync(projectId, id, request);
        return updated ? Ok() : NotFound();
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int projectId, int id)
    {
        var deleted = await issueTrackerService.DeleteConfigAsync(projectId, id);
        return deleted ? NoContent() : NotFound();
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost("{id:int}/test")]
    public async Task<IActionResult> Test(int projectId, int id)
    {
        var ok = await issueTrackerService.TestConfigAsync(projectId, id);
        return ok ? Ok(new { success = true }) : BadRequest(new { error = "Connection test failed. Check the API key/token and configuration." });
    }
}
