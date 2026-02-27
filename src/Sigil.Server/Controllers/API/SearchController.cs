using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
[Route("api/search")]
public class SearchController(ISearchService searchService) : SigilController
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int? projectId)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new { issues = Array.Empty<object>(), releases = Array.Empty<object>() });

        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var results = await searchService.SearchAsync(q, projectId);
        return Ok(results);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim is not null ? Guid.Parse(claim) : null;
    }
}
