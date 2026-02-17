using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Issues;
using Sigil.Domain.Enums;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class IssuesController(
    IIssueService issueService,
    IIssueActivityService activityService) : SigilController
{
    [HttpGet("api/projects/{projectId:int}/issues")]
    public async Task<IActionResult> List(
        int projectId,
        [FromQuery] IssueStatus? status,
        [FromQuery] Severity? level,
        [FromQuery] Priority? priority,
        [FromQuery] string? search,
        [FromQuery] Guid? assignedToId,
        [FromQuery] IssueSortBy sortBy = IssueSortBy.LastSeen,
        [FromQuery] bool sortDesc = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new IssueQueryParams
        {
            Status = status,
            Priority = priority,
            Level = level,
            Search = search,
            AssignedToId = assignedToId,
            SortBy = sortBy,
            SortDescending = sortDesc,
            Page = page,
            PageSize = Math.Clamp(pageSize, 1, 100)
        };

        return Ok(await issueService.GetIssueSummariesAsync(projectId, query));
    }

    [HttpGet("api/issues/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var detail = await issueService.GetIssueDetailAsync(id);
        return detail is not null ? Ok(detail) : NotFound();
    }

    [HttpPut("api/issues/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var userId = GetUserId();
        var issue = await issueService.UpdateIssueStatusAsync(id, request.Status, userId);
        return Ok(new { issue.Id, issue.Status });
    }

    [HttpPut("api/issues/{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignRequest request)
    {
        var userId = GetUserId();
        var issue = await issueService.AssignIssueAsync(id, request.UserId, userId);
        return Ok(new { issue.Id, issue.AssignedToId });
    }

    [HttpPut("api/issues/{id:int}/priority")]
    public async Task<IActionResult> UpdatePriority(int id, [FromBody] UpdatePriorityRequest request)
    {
        var issue = await issueService.UpdateIssuePriorityAsync(id, request.Priority);
        return Ok(new { issue.Id, issue.Priority });
    }

    [HttpDelete("api/issues/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await issueService.DeleteIssueAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("api/issues/{id:int}/activity")]
    public async Task<IActionResult> Activity(int id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        return Ok(await activityService.GetActivitySummariesAsync(id, page, Math.Clamp(pageSize, 1, 100)));
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim is not null ? Guid.Parse(claim) : null;
    }
}
