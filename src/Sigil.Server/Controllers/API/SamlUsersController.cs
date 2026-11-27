using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/saml/users")]
[Authorize(Policy = SigilPermissions.CanAccessAdmin)]
public class SamlUsersController(ISamlConnectionService connectionService) : SigilController
{
    [HttpGet("pending")]
    public async Task<IActionResult> Pending()
        => Ok(await connectionService.GetPendingUsersAsync());

    [HttpPost("{userId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid userId)
    {
        var approved = await connectionService.ApproveUserAsync(userId);
        return approved ? NoContent() : NotFound();
    }

    [HttpPost("{userId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid userId)
    {
        var rejected = await connectionService.RejectUserAsync(userId);
        return rejected ? NoContent() : NotFound();
    }
}
