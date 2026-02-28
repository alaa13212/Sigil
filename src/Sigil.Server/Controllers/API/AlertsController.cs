using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Alerts;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class AlertsController(IAlertService alertService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("api/projects/{projectId:int}/alert-rules")]
    public async Task<IActionResult> ListRules(int projectId) =>
        Ok(await alertService.GetRulesForProjectAsync(projectId));

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost("api/projects/{projectId:int}/alert-rules")]
    public async Task<IActionResult> CreateRule(int projectId, [FromBody] CreateAlertRuleRequest request)
    {
        var rule = await alertService.CreateRuleAsync(projectId, request);
        return CreatedAtAction(nameof(ListRules), new { projectId }, rule);
    }

    [HttpPut("api/alert-rules/{id:int}")]
    public async Task<IActionResult> UpdateRule(int id, [FromBody] UpdateAlertRuleRequest request)
    {
        var rule = await alertService.UpdateRuleAsync(id, request);
        return rule is not null ? Ok(rule) : NotFound();
    }

    [HttpDelete("api/alert-rules/{id:int}")]
    public async Task<IActionResult> DeleteRule(int id)
    {
        var deleted = await alertService.DeleteRuleAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPatch("api/alert-rules/{id:int}/toggle")]
    public async Task<IActionResult> ToggleRule(int id, [FromBody] ToggleRequest body)
    {
        var success = await alertService.ToggleRuleAsync(id, body.Enabled);
        return success ? Ok() : NotFound();
    }

    [HttpPost("api/alert-rules/{id:int}/test")]
    public async Task<IActionResult> TestRule(int id)
    {
        try
        {
            await alertService.SendTestAlertAsync(id);
            return Ok(new { message = "Test alert sent." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("api/projects/{projectId:int}/alert-history")]
    public async Task<IActionResult> History(
        int projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50) =>
        Ok(await alertService.GetAlertHistoryAsync(projectId, page, Math.Clamp(pageSize, 1, 100)));
}

public record ToggleRequest(bool Enabled);
