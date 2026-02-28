using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
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

    [Authorize(Policy = SigilPermissions.CanViewProject)]
    [HttpGet("{projectId:int}")]
    public async Task<IActionResult> Get(int projectId)
    {
        var detail = await projectService.GetProjectDetailAsync(projectId);
        return detail is not null ? Ok(detail) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        var project = await projectService.CreateProjectAsync(request.Name, request.Platform, request.TeamId);
        return CreatedAtAction(nameof(Get), new { projectId = project.Id },
            new ProjectResponse(project.Id, project.Name, project.Platform, project.ApiKey));
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPut("{projectId:int}")]
    public async Task<IActionResult> Update(int projectId, [FromBody] UpdateProjectRequest request)
    {
        var existing = await projectService.GetProjectByIdAsync(projectId);
        if (existing is null)
            return NotFound();

        var project = await projectService.UpdateProjectAsync(projectId, request.Name);
        return Ok(new ProjectResponse(project.Id, project.Name, project.Platform, project.ApiKey));
    }

    [Authorize(Policy = SigilPermissions.CanDeleteProject)]
    [HttpDelete("{projectId:int}")]
    public async Task<IActionResult> Delete(int projectId)
    {
        var deleted = await projectService.DeleteProjectAsync(projectId);
        return deleted ? NoContent() : NotFound();
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPost("{projectId:int}/rotate-key")]
    public async Task<IActionResult> RotateKey(int projectId)
    {
        var existing = await projectService.GetProjectByIdAsync(projectId);
        if (existing is null)
            return NotFound();

        var newKey = await projectService.RotateApiKeyAsync(projectId);
        return Ok(new { apiKey = newKey });
    }

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpGet("{projectId:int}/config")]
    public async Task<IActionResult> GetConfig(int projectId)
        => Ok(await projectConfigEditorService.GetAllAsync(projectId));

    [Authorize(Policy = SigilPermissions.CanManageProject)]
    [HttpPut("{projectId:int}/config/{key}")]
    public async Task<IActionResult> SetConfig(int projectId, string key, [FromBody] SetConfigValueRequest request)
    {
        await projectConfigEditorService.SetAsync(projectId, key, request.Value);
        return NoContent();
    }
}
