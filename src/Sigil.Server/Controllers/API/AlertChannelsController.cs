using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Alerts;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize(Policy = SigilPermissions.CanAccessAdmin)]
public class AlertChannelsController(IAlertChannelService channelService) : SigilController
{
    [HttpGet("api/alert-channels")]
    public async Task<IActionResult> List() =>
        Ok(await channelService.GetAllChannelsAsync());

    [HttpPost("api/alert-channels")]
    public async Task<IActionResult> Create([FromBody] CreateAlertChannelRequest request)
    {
        var channel = await channelService.CreateChannelAsync(request);
        return CreatedAtAction(nameof(List), channel);
    }

    [HttpPut("api/alert-channels/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateAlertChannelRequest request)
    {
        var channel = await channelService.UpdateChannelAsync(id, request);
        return channel is not null ? Ok(channel) : NotFound();
    }

    [HttpDelete("api/alert-channels/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await channelService.DeleteChannelAsync(id);
        return deleted ? NoContent() : Conflict(new { error = "Channel is in use by one or more alert rules and cannot be deleted." });
    }
}
