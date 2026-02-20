using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.AutoTags;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Server.Client.Services;

public class ApiAutoTagService(HttpClient http) : IAutoTagService
{
    public async Task<List<AutoTagRuleResponse>> GetRulesForProjectAsync(int projectId) =>
        await http.GetFromJsonAsync<List<AutoTagRuleResponse>>($"api/projects/{projectId}/auto-tags") ?? [];

    public async Task<AutoTagRuleResponse> CreateRuleAsync(int projectId, CreateAutoTagRuleRequest request)
    {
        var response = await http.PostAsJsonAsync($"api/projects/{projectId}/auto-tags", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AutoTagRuleResponse>())!;
    }

    public async Task<AutoTagRuleResponse?> UpdateRuleAsync(int ruleId, UpdateAutoTagRuleRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/auto-tags/{ruleId}", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AutoTagRuleResponse>();
    }

    public async Task<bool> DeleteRuleAsync(int ruleId)
    {
        var response = await http.DeleteAsync($"api/auto-tags/{ruleId}");
        return response.IsSuccessStatusCode;
    }

    public Task<List<AutoTagRule>> GetRawRulesForProjectAsync(int projectId) =>
        throw new NotSupportedException("Not available on client.");

    public void ApplyRules(ParsedEvent parsedEvent, List<AutoTagRule> rules) =>
        throw new NotSupportedException("Not available on client.");
}
