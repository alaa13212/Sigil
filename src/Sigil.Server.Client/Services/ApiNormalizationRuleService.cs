using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.NormalizationRules;
using Sigil.Domain.Entities;

namespace Sigil.Server.Client.Services;

public class ApiNormalizationRuleService(HttpClient http) : INormalizationRuleService
{
    public async Task<List<TextNormalizationRule>> GetRulesAsync(int projectId) =>
        await http.GetFromJsonAsync<List<TextNormalizationRule>>($"api/projects/{projectId}/normalization-rules") ?? [];

    public async Task<TextNormalizationRule> CreateRuleAsync(int projectId, CreateNormalizationRuleRequest request)
    {
        var response = await http.PostAsJsonAsync($"api/projects/{projectId}/normalization-rules", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TextNormalizationRule>())!;
    }

    public async Task<TextNormalizationRule?> UpdateRuleAsync(int ruleId, UpdateNormalizationRuleRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/normalization-rules/{ruleId}", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TextNormalizationRule>();
    }

    public async Task<bool> DeleteRuleAsync(int ruleId)
    {
        var response = await http.DeleteAsync($"api/normalization-rules/{ruleId}");
        return response.IsSuccessStatusCode;
    }

    public List<TextNormalizationRule> CreateDefaultRulesPreset() =>
        throw new NotSupportedException("Not available on client.");

    public Task<List<TextNormalizationRule>> GetRawRulesAsync(int projectId) =>
        throw new NotSupportedException("Not available on client.");
}
