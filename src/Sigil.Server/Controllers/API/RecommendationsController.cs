using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/projects/{projectId:int}/recommendations")]
[Authorize]
public class RecommendationsController(IRecommendationService recommendationService, IProjectService projectService) : SigilController
{
    [HttpGet("count")]
    public async Task<IActionResult> Count(int projectId)
    {
        return Ok(await recommendationService.GetRecommendationCountAsync(projectId));
    }

    [HttpGet]
    public async Task<IActionResult> List(int projectId)
    {
        return Ok(await recommendationService.GetRecommendationsAsync(projectId));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(int projectId)
    {
        Project? project = await projectService.GetProjectByIdAsync(projectId);
        if (project is null)
            return NotFound();
        
        await recommendationService.RunAnalyzersAsync(project);
        return Ok(await recommendationService.GetRecommendationsAsync(projectId));
    }

    [HttpPut("/api/recommendations/{id:int}/dismiss")]
    public async Task<IActionResult> Dismiss(int id)
    {
        await recommendationService.DismissAsync(id);
        return NoContent();
    }
}
