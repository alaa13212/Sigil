using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.AutoTags;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class AutoTagController(IAutoTagService autoTagService) : SigilController
{
    [HttpGet("api/projects/{projectId:int}/auto-tags")]
    public async Task<IActionResult> GetRules(int projectId)
    {
        var rules = await autoTagService.GetRulesForProjectAsync(projectId);
        return Ok(rules);
    }

    [HttpPost("api/projects/{projectId:int}/auto-tags")]
    public async Task<IActionResult> CreateRule(int projectId, [FromBody] CreateAutoTagRuleRequest request)
    {
        var rule = await autoTagService.CreateRuleAsync(projectId, request);
        return Ok(rule);
    }

    [HttpPut("api/auto-tags/{id:int}")]
    public async Task<IActionResult> UpdateRule(int id, [FromBody] UpdateAutoTagRuleRequest request)
    {
        var updated = await autoTagService.UpdateRuleAsync(id, request);
        return updated is not null ? Ok(updated) : NotFound();
    }

    [HttpDelete("api/auto-tags/{id:int}")]
    public async Task<IActionResult> DeleteRule(int id)
    {
        var deleted = await autoTagService.DeleteRuleAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
