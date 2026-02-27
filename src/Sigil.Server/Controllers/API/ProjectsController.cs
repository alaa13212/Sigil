using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController(IProjectService projectService, IProjectConfigEditorService projectConfigEditorService) : SigilController
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        return Ok(await projectService.GetProjectListAsync());
    }

    [HttpGet("overviews")]
    public async Task<IActionResult> Overviews()
    {
        return Ok(await projectService.GetAllProjectOverviewsAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var detail = await projectService.GetProjectDetailAsync(id);
        return detail is not null ? Ok(detail) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        var project = await projectService.CreateProjectAsync(request.Name, request.Platform, request.TeamId);
        return CreatedAtAction(nameof(Get), new { id = project.Id },
            new ProjectResponse(project.Id, project.Name, project.Platform, project.ApiKey));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProjectRequest request)
    {
        var existing = await projectService.GetProjectByIdAsync(id);
        if (existing is null)
            return NotFound();

        var project = await projectService.UpdateProjectAsync(id, request.Name);
        return Ok(new ProjectResponse(project.Id, project.Name, project.Platform, project.ApiKey));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await projectService.DeleteProjectAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/rotate-key")]
    public async Task<IActionResult> RotateKey(int id)
    {
        var existing = await projectService.GetProjectByIdAsync(id);
        if (existing is null)
            return NotFound();

        var newKey = await projectService.RotateApiKeyAsync(id);
        return Ok(new { apiKey = newKey });
    }

    [HttpGet("{id:int}/config")]
    public async Task<IActionResult> GetConfig(int id)
        => Ok(await projectConfigEditorService.GetAllAsync(id));

    [HttpPut("{id:int}/config/{key}")]
    public async Task<IActionResult> SetConfig(int id, string key, [FromBody] SetConfigValueRequest request)
    {
        await projectConfigEditorService.SetAsync(id, key, request.Value);
        return NoContent();
    }
}
