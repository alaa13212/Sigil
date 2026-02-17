using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence;

internal class ProjectService(SigilDbContext dbContext, IAppConfigService appConfigService) : IProjectService
{
    public async Task<Project> CreateProjectAsync(string name, Platform platform)
    {
        var project = new Project
        {
            Name = name,
            Platform = platform,
            ApiKey = RandomNumberGenerator.GetHexString(32)
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        return project;
    }

    public async Task<Project?> GetProjectByIdAsync(int id)
    {
        return await dbContext.Projects.FindAsync(id);
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        return await dbContext.Projects.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<Project?> GetProjectByApiKeyAsync(string apiKey)
    {
        return await dbContext.Projects.FirstOrDefaultAsync(p => p.ApiKey == apiKey);
    }

    public async Task<Project> UpdateProjectAsync(int projectId, string name)
    {
        var project = await dbContext.Projects.AsTracking().FirstAsync(p => p.Id == projectId);
        project.Name = name;
        await dbContext.SaveChangesAsync();
        return project;
    }

    public async Task<bool> DeleteProjectAsync(int projectId)
    {
        return await dbContext.Projects.Where(p => p.Id == projectId).ExecuteDeleteAsync() > 0;
    }

    public async Task<string> RotateApiKeyAsync(int projectId)
    {
        var project = await dbContext.Projects.AsTracking().FirstAsync(p => p.Id == projectId);
        project.ApiKey = RandomNumberGenerator.GetHexString(32);
        await dbContext.SaveChangesAsync();
        return project.ApiKey;
    }

    public async Task<List<ProjectResponse>> GetProjectListAsync()
    {
        var projects = await GetAllProjectsAsync();
        return projects.Select(p => new ProjectResponse(p.Id, p.Name, p.Platform, p.ApiKey)).ToList();
    }

    public async Task<ProjectDetailResponse?> GetProjectDetailAsync(int id)
    {
        var project = await GetProjectByIdAsync(id);
        if (project is null) return null;

        var hostUrl = await appConfigService.GetAsync("host_url");
        var dsn = !string.IsNullOrEmpty(hostUrl)
            ? $"{hostUrl.TrimEnd('/')}/api/{project.Id}"
            : "";

        return new ProjectDetailResponse(project.Id, project.Name, project.Platform, project.ApiKey, dsn, project.TeamId);
    }

    public async Task<ProjectOverviewResponse?> GetProjectOverviewAsync(int id)
    {
        var project = await dbContext.Projects
            .Where(p => p.Id == id)
            .Select(p => new ProjectOverviewResponse(
                p.Id, p.Name, p.Platform,
                p.Issues.Count,
                p.Events.Count))
            .FirstOrDefaultAsync();

        return project;
    }
}
