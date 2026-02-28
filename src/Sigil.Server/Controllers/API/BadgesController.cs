using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
[Route("api/badges")]
public class BadgesController(IBadgeService badgeService) : SigilController
{
    [HttpGet]
    public async Task<IActionResult> GetBadges()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var counts = await badgeService.GetAllBadgeCountsAsync(userId.Value);
        return Ok(counts);
    }

}
