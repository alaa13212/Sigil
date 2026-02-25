using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;
using Sigil.Domain;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Persistence;

internal class ProjectService(SigilDbContext dbContext, IAppConfigService appConfigService, INormalizationRuleService normalizationRuleService, IProjectCache projectCache) : IProjectService
{
    public async Task<Project> CreateProjectAsync(string name, Platform platform, int? teamId = null)
    {
        var project = new Project
        {
            Name = name,
            Platform = platform,
            ApiKey = RandomNumberGenerator.GetHexString(32).ToLower(),
            TeamId = teamId,
            Rules = normalizationRuleService.CreateDefaultRulesPreset()
        };
        project.Rules.ForEach(r => r.Project = project);

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        projectCache.Set(project);
        projectCache.InvalidateList();
        return project;
    }

    public async Task<Project?> GetProjectByIdAsync(int id)
    {
        if (projectCache.TryGet(id, out var cached))
            return cached;

        var project = await dbContext.Projects.FindAsync(id);
        if (project is not null)
            projectCache.Set(project);
        return project;
    }

    
    public async Task<List<Project>> GetAllProjectsAsync()
    {
        return await dbContext.Projects.ToListAsync();
    }

    public async Task<Project> UpdateProjectAsync(int projectId, string name)
    {
        var project = await dbContext.Projects.AsTracking().FirstAsync(p => p.Id == projectId);
        project.Name = name;
        await dbContext.SaveChangesAsync();
        projectCache.Invalidate(projectId);
        projectCache.InvalidateList();
        return project;
    }

    public async Task<bool> DeleteProjectAsync(int projectId)
    {
        var deleted = await dbContext.Projects.Where(p => p.Id == projectId).ExecuteDeleteAsync() > 0;
        if (deleted)
        {
            projectCache.Invalidate(projectId);
            projectCache.InvalidateList();
        }
        return deleted;
    }

    public async Task<string> RotateApiKeyAsync(int projectId)
    {
        var project = await dbContext.Projects.AsTracking().FirstAsync(p => p.Id == projectId);
        project.ApiKey = RandomNumberGenerator.GetHexString(32).ToLower();
        await dbContext.SaveChangesAsync();
        projectCache.Invalidate(projectId);
        projectCache.InvalidateList();
        return project.ApiKey;
    }

    public async Task<List<ProjectResponse>> GetProjectListAsync()
    {
        if (projectCache.TryGetList(out var cached) && cached is not null)
            return cached.Select(p => new ProjectResponse(p.Id, p.Name, p.Platform, p.ApiKey)).ToList();

        var projects = await dbContext.Projects.OrderBy(p => p.Name).ToListAsync();
        projectCache.SetList(projects);
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
                p.Issues.Count(i => i.MergeSetId == null || i.MergeSet!.PrimaryIssueId == i.Id),
                p.Events.Count))
            .ToListAsync();
    }
}
