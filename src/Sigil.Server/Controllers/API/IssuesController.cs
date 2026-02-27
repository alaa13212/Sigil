using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Issues;
using Sigil.Domain.Enums;
using Sigil.Server.Framework;
using PageType = Sigil.Domain.Enums.PageType;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class IssuesController(
    IIssueService issueService,
    IIssueActivityService activityService,
    IProjectService projectService,
    IBookmarkService bookmarkService) : SigilController
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
        [FromQuery] int pageSize = 50,
        [FromQuery] bool bookmarked = false,
        [FromQuery] bool includeViewedInfo = false)
    {
        if (await projectService.GetProjectByIdAsync(projectId) is null)
            return NotFound();

        var userId = GetUserId();
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
            PageSize = Math.Clamp(pageSize, 1, 100),
            BookmarkedByUserId = bookmarked ? userId : null,
            ViewerUserId = includeViewedInfo ? userId : null
        };

        var summaries = await issueService.GetIssueSummariesAsync(projectId, query);

        if (userId.HasValue)
            await issueService.RecordPageViewAsync(userId.Value, projectId, PageType.Issues);

        return Ok(summaries);
    }

    [HttpGet("api/issues/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var detail = await issueService.GetIssueDetailAsync(id);
        if (detail is null) return NotFound();

        var userId = GetUserId();
        if (userId.HasValue)
            await bookmarkService.RecordIssueViewAsync(id, userId.Value);

        return Ok(detail);
    }

    [HttpPut("api/issues/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var userId = GetUserId();
        var issue = await issueService.UpdateIssueStatusAsync(id, request.Status, userId, request.IgnoreFutureEvents);
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
        Guid? userId = GetUserId();
        var issue = await issueService.UpdateIssuePriorityAsync(id, request.Priority, userId);
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

    [HttpGet("api/issues/{id:int}/similar")]
    public async Task<IActionResult> Similar(int id)
    {
        return Ok(await issueService.GetSimilarIssuesAsync(id));
    }

    [HttpPost("api/issues/{id:int}/comments")]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Comment cannot be empty.");

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var activity = await activityService.LogActivityAsync(id, IssueActivityAction.Commented, userId.Value, request.Message.Trim());
        return Ok(new ActivityResponse(activity.Id, activity.Action, activity.Message, activity.Timestamp, null, userId.Value));
    }

    [HttpGet("api/issues/{id:int}/histogram")]
    public async Task<IActionResult> GetHistogram(int id, [FromQuery] int days = 14)
    {
        return Ok(await issueService.GetHistogramAsync(id, Math.Clamp(days, 1, 90)));
    }

    [HttpPost("api/issues/histogram/bulk")]
    public async Task<IActionResult> GetBulkHistograms([FromBody] List<int> issueIds, [FromQuery] int days = 14)
    {
        if (issueIds.Count == 0) return Ok(new Dictionary<int, List<int>>());
        return Ok(await issueService.GetBulkHistogramsAsync(issueIds, Math.Clamp(days, 1, 90)));
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim is not null ? Guid.Parse(claim) : null;
    }
}
