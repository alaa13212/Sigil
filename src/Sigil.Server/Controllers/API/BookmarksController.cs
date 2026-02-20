using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class BookmarksController(IBookmarkService bookmarkService) : SigilController
{
    [HttpPost("api/issues/{id:int}/bookmark")]
    public async Task<IActionResult> Toggle(int id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var isBookmarked = await bookmarkService.ToggleBookmarkAsync(id, userId.Value);
        return Ok(new { isBookmarked });
    }

    [HttpGet("api/issues/{id:int}/bookmark")]
    public async Task<IActionResult> Status(int id)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var isBookmarked = await bookmarkService.IsBookmarkedAsync(id, userId.Value);
        return Ok(new { isBookmarked });
    }

    [HttpGet("api/bookmarks")]
    public async Task<IActionResult> List()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var issues = await bookmarkService.GetBookmarkedIssuesAsync(userId.Value);
        return Ok(issues);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim is not null ? Guid.Parse(claim) : null;
    }
}
