using Sigil.Application.Models.Projects;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Application.Interfaces;

public interface IProjectService
{
    // Entity access
    Task<Project?> GetProjectByIdAsync(int id);
    Task<List<Project>> GetAllProjectsAsync();
    Task<Project?> GetProjectByApiKeyAsync(string apiKey);

    // DTO access (UI/API)
    Task<List<ProjectResponse>> GetProjectListAsync();
    Task<ProjectDetailResponse?> GetProjectDetailAsync(int id);
    Task<ProjectOverviewResponse?> GetProjectOverviewAsync(int id);

    // Mutations
    Task<Project> CreateProjectAsync(string name, Platform platform);
    Task<Project> UpdateProjectAsync(int projectId, string name);
    Task<bool> DeleteProjectAsync(int projectId);
    Task<string> RotateApiKeyAsync(int projectId);
}
