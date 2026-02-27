using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Sigil.Server.Framework;

public abstract class SigilController : ControllerBase
{
    [NonAction]
    protected IActionResult TooManyRequests(TimeSpan? retryAfter = null)
    {
        return new TooManyRequestsResult(retryAfter);
    }
    
    [NonAction]
    protected Guid? GetUserId()
    {
        string? claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim is not null ? Guid.Parse(claim) : null;
    }
}