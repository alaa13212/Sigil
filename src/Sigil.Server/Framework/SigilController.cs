using Microsoft.AspNetCore.Mvc;

namespace Sigil.Server.Framework;

public abstract class SigilController : ControllerBase
{
    [NonAction]
    protected IActionResult TooManyRequests(TimeSpan? retryAfter = null)
    {
        return new TooManyRequestsResult(retryAfter);
    }
}