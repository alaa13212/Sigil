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
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTrackerConfigRequest request)
    {
        var updated = await issueTrackerService.UpdateConfigAsync(id, request);
        return updated ? Ok() : NotFound();
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await issueTrackerService.DeleteConfigAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost("{id:int}/test")]
    public async Task<IActionResult> Test(int id)
    {
        var ok = await issueTrackerService.TestConfigAsync(id);
        return ok ? Ok(new { success = true }) : BadRequest(new { error = "Connection test failed. Check the API key/token and configuration." });
    }
}

// Non-scoped endpoints for when only the config ID is known
[ApiController]
[Authorize]
[Route("api/issue-tracker-configs")]
public class IssueTrackerConfigsByIdController(IIssueTrackerService issueTrackerService) : SigilController
{
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTrackerConfigRequest request)
    {
        var updated = await issueTrackerService.UpdateConfigAsync(id, request);
        return updated ? Ok() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await issueTrackerService.DeleteConfigAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/test")]
    public async Task<IActionResult> Test(int id)
    {
        var ok = await issueTrackerService.TestConfigAsync(id);
        return ok ? Ok(new { success = true }) : BadRequest(new { error = "Connection test failed." });
    }
}
