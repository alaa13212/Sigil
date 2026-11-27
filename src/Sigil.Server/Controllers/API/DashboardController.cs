using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Common;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IEventService eventService) : SigilController
{
    [HttpGet("activity")]
    public async Task<IActionResult> Activity(
        [FromQuery] int days = 7,
        [FromQuery] TimeSeriesGranularity granularity = TimeSeriesGranularity.Auto)
    {
        return Ok(await eventService.GetDashboardEventSeriesAsync(Math.Clamp(days, 1, 90), granularity));
    }

    [HttpGet("sparklines")]
    public async Task<IActionResult> Sparklines(
        [FromQuery] int days = 7,
        [FromQuery] TimeSeriesGranularity granularity = TimeSeriesGranularity.Auto)
    {
        return Ok(await eventService.GetBulkProjectEventSeriesAsync(Math.Clamp(days, 1, 90), granularity));
    }
}
