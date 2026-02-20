using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.MergeSets;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class MergeSetsController(IMergeSetService mergeSetService) : SigilController
{
    [HttpPost("api/projects/{projectId:int}/merge-sets")]
    public async Task<IActionResult> Create(int projectId, [FromBody] CreateMergeSetRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await mergeSetService.CreateAsync(projectId, request.IssueIds, userId.Value);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("api/merge-sets/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var result = await mergeSetService.GetByIdAsync(id);
        return result is not null ? Ok(result) : NotFound();
    }

    [HttpPost("api/merge-sets/{id:int}/issues")]
    public async Task<IActionResult> AddIssue(int id, [FromBody] AddIssueToMergeSetRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            var result = await mergeSetService.AddIssueAsync(id, request.IssueId, userId.Value);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("api/merge-sets/{id:int}/issues/{issueId:int}")]
    public async Task<IActionResult> RemoveIssue(int id, int issueId)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        try
        {
            await mergeSetService.RemoveIssueAsync(id, issueId, userId.Value);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("api/merge-sets/{id:int}/primary")]
    public async Task<IActionResult> SetPrimary(int id, [FromBody] SetPrimaryRequest request)
    {
        try
        {
            await mergeSetService.SetPrimaryAsync(id, request.IssueId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim is not null ? Guid.Parse(claim) : null;
    }
}
