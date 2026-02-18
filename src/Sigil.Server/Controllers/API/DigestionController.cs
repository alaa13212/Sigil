using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController, Authorize]
public class DigestionController(IDigestionMonitorService monitorService) : SigilController
{
    [HttpGet("api/admin/digestion/stats")]
    public async Task<IActionResult> GetStats()
        => Ok(await monitorService.GetStatsAsync());

    [HttpGet("api/admin/digestion/failures")]
    public async Task<IActionResult> GetRecentFailures([FromQuery] int limit = 50)
        => Ok(await monitorService.GetRecentFailuresAsync(limit));
}
