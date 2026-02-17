using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Teams;
using Sigil.Domain.Enums;

namespace Sigil.Server.Client.Services;

public class ApiTeamService(HttpClient http) : ITeamService
{
    public async Task<List<TeamResponse>> GetTeamsAsync()
    {
        return await http.GetFromJsonAsync<List<TeamResponse>>("api/teams") ?? [];
    }

    public async Task<TeamDetailResponse?> GetTeamDetailAsync(int teamId)
    {
        try
        {
            return await http.GetFromJsonAsync<TeamDetailResponse>($"api/teams/{teamId}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<TeamResponse> CreateTeamAsync(string name, Guid creatorUserId)
    {
        var response = await http.PostAsJsonAsync("api/teams", new CreateTeamRequest(name));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeamResponse>())!;
    }

    public async Task<TeamResponse?> UpdateTeamAsync(int teamId, string name)
    {
        var response = await http.PutAsJsonAsync($"api/teams/{teamId}", new CreateTeamRequest(name));
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TeamResponse>();
    }

    public async Task<bool> DeleteTeamAsync(int teamId)
    {
        var response = await http.DeleteAsync($"api/teams/{teamId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> AddMemberAsync(int teamId, Guid userId, TeamRole role)
    {
        var response = await http.PostAsJsonAsync($"api/teams/{teamId}/members",
            new AddTeamMemberRequest(userId, role));
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveMemberAsync(int teamId, Guid userId)
    {
        var response = await http.DeleteAsync($"api/teams/{teamId}/members/{userId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateMemberRoleAsync(int teamId, Guid userId, TeamRole role)
    {
        var response = await http.PutAsJsonAsync($"api/teams/{teamId}/members/{userId}",
            new { Role = role });
        return response.IsSuccessStatusCode;
    }
}
