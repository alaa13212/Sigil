using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Shared;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
public class SharedLinksController(ISharedLinkService sharedLinkService, IAppConfigService appConfigService) : SigilController
{
    [Authorize]
    [HttpPost("api/issues/{issueId:int}/share")]
    public async Task<IActionResult> CreateLink(int issueId, [FromBody] CreateSharedLinkRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var link = await sharedLinkService.CreateLinkAsync(issueId, userId.Value, appConfigService.HostUrl ?? string.Empty, request.Duration);
        return Ok(link);
    }

    [AllowAnonymous]
    [HttpGet("api/shared/{token:guid}")]
    public async Task<IActionResult> GetSharedIssue(Guid token)
    {
        var result = await sharedLinkService.ValidateLinkAsync(token);
        return result is not null ? Ok(result) : NotFound(new { error = "Link not found or has expired." });
    }

    [AllowAnonymous]
    [HttpGet("api/shared/{token:guid}/events")]
    public async Task<IActionResult> GetSharedEvents(Guid token, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await sharedLinkService.GetSharedEventsAsync(token, page, pageSize);
        return result is not null ? Ok(result) : NotFound(new { error = "Link not found or has expired." });
    }

    [AllowAnonymous]
    [HttpGet("api/shared/{token:guid}/events/{eventId:long}")]
    public async Task<IActionResult> GetSharedEventDetail(Guid token, long eventId)
    {
        var result = await sharedLinkService.GetSharedEventDetailAsync(token, eventId);
        return result is not null ? Ok(result) : NotFound(new { error = "Event not found." });
    }

    [Authorize]
    [HttpDelete("api/shared/{token:guid}")]
    public async Task<IActionResult> RevokeLink(Guid token)
    {
        var revoked = await sharedLinkService.RevokeLinkAsync(token);
        return revoked ? NoContent() : NotFound();
    }
}
