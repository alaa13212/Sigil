using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.IssueTrackers;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Integrations;

internal class LinearIssueTrackerClient : IIssueTrackerClient
{
    public TrackerType TrackerType => TrackerType.Linear;

    public async Task<ExternalIssueResult> CreateIssueAsync(TrackerConfig config, CreateExternalIssueRequest request)
    {
        var cfg = ParseConfig(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.ApiKey);

        const string mutation = @"
            mutation CreateIssue($teamId: String!, $title: String!, $description: String!) {
              issueCreate(input: { teamId: $teamId, title: $title, description: $description }) {
                issue { id identifier url state { name } }
              }
            }";

        var variables = new
        {
            teamId = cfg.TeamId,
            title = request.Title,
            description = $"{request.Description}\n\n[View in Sigil]({request.IssueUrl})"
        };

        var response = await http.PostAsJsonAsync("https://api.linear.app/graphql", new { query = mutation, variables });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LinearCreateResponse>(JsonOptions);
        var issue = result?.Data?.IssueCreate?.Issue
            ?? throw new InvalidOperationException("Linear issue creation failed.");

        return new ExternalIssueResult(issue.Id, issue.Url, issue.State?.Name);
    }

    public async Task<ExternalIssueStatus?> GetStatusAsync(TrackerConfig config, string externalId)
    {
        var cfg = ParseConfig(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.ApiKey);

        const string query = @"
            query GetIssue($id: String!) {
              issue(id: $id) { id identifier state { name type } }
            }";

        var response = await http.PostAsJsonAsync("https://api.linear.app/graphql",
            new { query, variables = new { id = externalId } });

        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<LinearIssueQueryResponse>(JsonOptions);
        var issue = result?.Data?.Issue;
        if (issue?.State == null) return null;

        var isDone = issue.State.Type is "completed" or "cancelled";
        return new ExternalIssueStatus(externalId, issue.State.Name, isDone);
    }

    public async Task<bool> CloseIssueAsync(TrackerConfig config, string externalId)
    {
        var cfg = ParseConfig(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.ApiKey);

        // Find the first "completed" workflow state for the issue's team
        const string stateQuery = @"
            query GetCompletedState($issueId: String!) {
              issue(id: $issueId) {
                team {
                  states { nodes { id type } }
                }
              }
            }";

        var stateResponse = await http.PostAsJsonAsync("https://api.linear.app/graphql",
            new { query = stateQuery, variables = new { issueId = externalId } });
        if (!stateResponse.IsSuccessStatusCode) return false;

        var stateResult = await stateResponse.Content.ReadFromJsonAsync<LinearCompletedStateQueryResponse>(JsonOptions);
        var completedStateId = stateResult?.Data?.Issue?.Team?.States?.Nodes
            ?.FirstOrDefault(s => s.Type == "completed")?.Id;
        if (completedStateId == null) return false;

        const string updateMutation = @"
            mutation CloseIssue($id: String!, $stateId: String!) {
              issueUpdate(id: $id, input: { stateId: $stateId }) { success }
            }";

        var updateResponse = await http.PostAsJsonAsync("https://api.linear.app/graphql",
            new { query = updateMutation, variables = new { id = externalId, stateId = completedStateId } });
        return updateResponse.IsSuccessStatusCode;
    }

    public async Task<bool> TestConnectionAsync(TrackerConfig config)
    {
        var cfg = ParseConfig(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.ApiKey);

        const string query = "query { viewer { id } }";
        var response = await http.PostAsJsonAsync("https://api.linear.app/graphql", new { query });
        return response.IsSuccessStatusCode;
    }

    private static HttpClient CreateHttpClient(string apiKey)
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    private static LinearTrackerConfig ParseConfig(string json) =>
        JsonSerializer.Deserialize<LinearTrackerConfig>(json, JsonOptions)
        ?? throw new InvalidOperationException("Invalid Linear tracker config.");

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record LinearTrackerConfig(string ApiKey, string TeamId);

    private record LinearCreateResponse(LinearCreateData? Data);
    private record LinearCreateData([property: JsonPropertyName("issueCreate")] LinearIssueCreate? IssueCreate);
    private record LinearIssueCreate(LinearIssueItem? Issue);
    private record LinearIssueItem(string Id, string Identifier, string Url, LinearState? State);

    private record LinearIssueQueryResponse(LinearIssueQueryData? Data);
    private record LinearIssueQueryData(LinearIssueItem? Issue);
    private record LinearState(string Name, string Type);

    private record LinearCompletedStateQueryResponse(LinearCompletedStateData? Data);
    private record LinearCompletedStateData(LinearIssueWithTeam? Issue);
    private record LinearIssueWithTeam(LinearTeam? Team);
    private record LinearTeam(LinearStateNodes? States);
    private record LinearStateNodes(List<LinearStateItem>? Nodes);
    private record LinearStateItem(string Id, string Type);
}
