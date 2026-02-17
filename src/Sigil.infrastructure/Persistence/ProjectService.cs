using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;
using Sigil.Domain;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence;

internal class ProjectService(SigilDbContext dbContext, IAppConfigService appConfigService) : IProjectService
{
    public async Task<Project> CreateProjectAsync(string name, Platform platform, int? teamId = null)
    {
        var project = new Project
        {
            Name = name,
            Platform = platform,
            ApiKey = RandomNumberGenerator.GetHexString(32).ToLower(),
            TeamId = teamId
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        return project;
    }

    public async Task<Project?> GetProjectByIdAsync(int id)
    {
        return await dbContext.Projects.FindAsync(id);
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
        project.ApiKey = RandomNumberGenerator.GetHexString(32).ToLower();
        await dbContext.SaveChangesAsync();
        return project.ApiKey;
    }

    public async Task<List<ProjectResponse>> GetProjectListAsync()
    {
        var projects = await dbContext.Projects.OrderBy(p => p.Name).ToListAsync();
        return projects.Select(p => new ProjectResponse(p.Id, p.Name, p.Platform, p.ApiKey)).ToList();
    }

    public async Task<ProjectDetailResponse?> GetProjectDetailAsync(int id)
    {
        var project = await GetProjectByIdAsync(id);
        if (project is null) return null;

        var hostUrl = await appConfigService.GetAsync(AppConfigKeys.HostUrl);
        var dsn = "";
        if (!string.IsNullOrEmpty(hostUrl))
        {
            var uri = new Uri(hostUrl.TrimEnd('/'));
            dsn = $"{uri.Scheme}://{project.ApiKey}@{uri.Authority}/{project.Id}";
        }

        return new ProjectDetailResponse(project.Id, project.Name, project.Platform, project.ApiKey, dsn, project.TeamId);
    }

    public async Task<List<ProjectOverviewResponse>> GetAllProjectOverviewsAsync()
    {
        return await dbContext.Projects
            .OrderBy(p => p.Events.Max(e => e.Timestamp))
            .Select(p => new ProjectOverviewResponse(
                p.Id, p.Name, p.Platform,
                p.Issues.Count,
                p.Events.Count))
            .ToListAsync();
    }
}
