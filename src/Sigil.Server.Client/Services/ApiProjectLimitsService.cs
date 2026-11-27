using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;

namespace Sigil.Server.Client.Services;

public class ApiProjectLimitsService(HttpClient http) : IProjectLimitsService
{
    public async Task<ProjectLimitsResponse> GetAsync(int projectId) =>
        await http.GetFromJsonAsync<ProjectLimitsResponse>($"api/projects/{projectId}/limits")
        ?? throw new InvalidOperationException("The project limits could not be read.");
}
