using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Alerts;
using Sigil.Domain.Entities;

namespace Sigil.Server.Client.Services;

public class ApiAlertService(HttpClient http) : IAlertService
{
    public async Task<List<AlertRuleResponse>> GetRulesForProjectAsync(int projectId) =>
        await http.GetFromJsonAsync<List<AlertRuleResponse>>($"api/projects/{projectId}/alert-rules") ?? [];

    public async Task<AlertRuleResponse> CreateRuleAsync(int projectId, CreateAlertRuleRequest request)
    {
        var response = await http.PostAsJsonAsync($"api/projects/{projectId}/alert-rules", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AlertRuleResponse>())!;
    }

    public async Task<AlertRuleResponse?> UpdateRuleAsync(int ruleId, UpdateAlertRuleRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/alert-rules/{ruleId}", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AlertRuleResponse>();
    }

    public async Task<bool> DeleteRuleAsync(int ruleId)
    {
        var response = await http.DeleteAsync($"api/alert-rules/{ruleId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ToggleRuleAsync(int ruleId, bool enabled)
    {
        var response = await http.PatchAsJsonAsync($"api/alert-rules/{ruleId}/toggle", new { enabled });
        return response.IsSuccessStatusCode;
    }

    public async Task SendTestAlertAsync(int ruleId)
    {
        var response = await http.PostAsync($"api/alert-rules/{ruleId}/test", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<PagedResponse<AlertHistoryResponse>> GetAlertHistoryAsync(int projectId, int page = 1, int pageSize = 50) =>
        await http.GetFromJsonAsync<PagedResponse<AlertHistoryResponse>>(
            $"api/projects/{projectId}/alert-history?page={page}&pageSize={pageSize}")
        ?? new PagedResponse<AlertHistoryResponse>([], 0, page, pageSize);

    // Server-only evaluation methods
    public Task EvaluateNewIssueAsync(Issue issue) => throw new NotSupportedException();
    public Task EvaluateRegressionAsync(Issue issue) => throw new NotSupportedException();
    public Task EvaluateThresholdAsync(Issue issue) => throw new NotSupportedException();
}
