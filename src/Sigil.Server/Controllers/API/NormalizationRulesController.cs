using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.NormalizationRules;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
public class NormalizationRulesController(INormalizationRuleService service) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("api/projects/{projectId:int}/normalization-rules")]
    public async Task<IActionResult> List(int projectId)
    {
        return Ok(await service.GetRulesAsync(projectId));
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost("api/projects/{projectId:int}/normalization-rules")]
    public async Task<IActionResult> Create(int projectId, [FromBody] CreateNormalizationRuleRequest request)
    {
        return Ok(await service.CreateRuleAsync(projectId, request));
    }

    [HttpPut("api/normalization-rules/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateNormalizationRuleRequest request)
    {
        var result = await service.UpdateRuleAsync(id, request);
        return result is not null ? Ok(result) : NotFound();
    }

    [HttpDelete("api/normalization-rules/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await service.DeleteRuleAsync(id);
        return deleted ? NoContent() : NotFound();
    }

}
