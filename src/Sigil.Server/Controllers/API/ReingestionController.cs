using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class ReingestionController(IReingestionService reingestionService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost("api/projects/{projectId:int}/reingest")]
    public async Task<IActionResult> StartProjectReingestion(int projectId)
    {
        try
        {
            var userId = GetUserId();
            var job = await reingestionService.StartProjectReingestionAsync(projectId, userId);
            return Ok(job);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [Authorize(Policy = SigilPermissions.CanEditIssue)]
    [HttpPost("api/issues/{issueId:int}/reingest")]
    public async Task<IActionResult> StartIssueReingestion(int issueId)
    {
        try
        {
            var userId = GetUserId();
            var job = await reingestionService.StartIssueReingestionAsync(issueId, userId);
            return Ok(job);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("api/reingestion/{jobId:int}")]
    public async Task<IActionResult> GetJobStatus(int jobId)
    {
        var job = await reingestionService.GetJobStatusAsync(jobId);
        return job is not null ? Ok(job) : NotFound();
    }

    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("api/projects/{projectId:int}/reingestion")]
    public async Task<IActionResult> GetJobsForProject(int projectId)
    {
        var jobs = await reingestionService.GetJobsForProjectAsync(projectId);
        return Ok(jobs);
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost("api/reingestion/{jobId:int}/cancel")]
    public async Task<IActionResult> CancelJob(int jobId)
    {
        var cancelled = await reingestionService.CancelJobAsync(jobId);
        return cancelled ? Ok() : NotFound();
    }
}
