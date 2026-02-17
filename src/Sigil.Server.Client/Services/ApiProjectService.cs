using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Server.Client.Services;

public class ApiProjectService(HttpClient http) : IProjectService
{
    // DTO access (UI/API)
    public async Task<List<ProjectResponse>> GetProjectListAsync()
    {
        return await http.GetFromJsonAsync<List<ProjectResponse>>("api/projects") ?? [];
    }

    public async Task<ProjectDetailResponse?> GetProjectDetailAsync(int id)
    {
        try
        {
            return await http.GetFromJsonAsync<ProjectDetailResponse>($"api/projects/{id}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<ProjectOverviewResponse?> GetProjectOverviewAsync(int id)
    {
        try
        {
            return await http.GetFromJsonAsync<ProjectOverviewResponse>($"api/projects/{id}/overview");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    // Entity access (delegates to DTO methods)
    public async Task<List<Project>> GetAllProjectsAsync()
    {
        var items = await GetProjectListAsync();
        return items.Select(ToEntity).ToList();
    }

    public async Task<Project?> GetProjectByIdAsync(int id)
    {
        var detail = await GetProjectDetailAsync(id);
        return detail is null ? null : new Project
        {
            Id = detail.Id,
            Name = detail.Name,
            Platform = detail.Platform,
            ApiKey = detail.ApiKey,
            TeamId = detail.TeamId
        };
    }

    public async Task<Project> CreateProjectAsync(string name, Platform platform)
    {
        var response = await http.PostAsJsonAsync("api/projects", new CreateProjectRequest(name, platform));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        return ToEntity(result!);
    }

    public async Task<Project> UpdateProjectAsync(int projectId, string name)
    {
        var response = await http.PutAsJsonAsync($"api/projects/{projectId}", new UpdateProjectRequest(name));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ProjectResponse>();
        return ToEntity(result!);
    }

    public async Task<bool> DeleteProjectAsync(int projectId)
    {
        var response = await http.DeleteAsync($"api/projects/{projectId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<string> RotateApiKeyAsync(int projectId)
    {
        var response = await http.PostAsync($"api/projects/{projectId}/rotate-key", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RotateKeyResponse>();
        return result!.ApiKey;
    }

    public Task<Project?> GetProjectByApiKeyAsync(string apiKey) =>
        throw new NotSupportedException("Not available on client.");

    private static Project ToEntity(ProjectResponse r) =>
        new() { Id = r.Id, Name = r.Name, Platform = r.Platform, ApiKey = r.ApiKey };

    private record RotateKeyResponse(string ApiKey);
}
