using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.IssueTrackers;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Integrations;

internal class JiraIssueTrackerClient(IHttpClientFactory httpClientFactory)
    : TrackerClientBase(httpClientFactory), IIssueTrackerClient
{
    public TrackerType TrackerType => TrackerType.Jira;

    public async Task<ExternalIssueResult> CreateIssueAsync(TrackerConfig config, CreateExternalIssueRequest request)
    {
        var cfg = ParseConfig<JiraTrackerConfig>(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg);

        var body = new
        {
            fields = new
            {
                project = new { key = cfg.ProjectKey },
                summary = request.Title,
                description = new
                {
                    type = "doc", version = 1,
                    content = new[] { new { type = "paragraph", content = new[] { new { type = "text", text = $"{request.Description}\n\nSigil: {request.IssueUrl}" } } } }
                },
                issuetype = new { name = "Bug" }
            }
        };

        var response = await http.PostAsJsonAsync($"{cfg.BaseUrl.TrimEnd('/')}/rest/api/3/issue", body);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JiraCreateResponse>(CamelCaseOptions);
        var issueUrl = $"{cfg.BaseUrl.TrimEnd('/')}/browse/{result!.Key}";
        return new ExternalIssueResult(result.Key, issueUrl, "Open");
    }

    public async Task<ExternalIssueStatus?> GetStatusAsync(TrackerConfig config, string externalId)
    {
        var cfg = ParseConfig<JiraTrackerConfig>(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg);

        var response = await http.GetAsync($"{cfg.BaseUrl.TrimEnd('/')}/rest/api/3/issue/{externalId}?fields=status");
        if (!response.IsSuccessStatusCode) return null;

        var issue = await response.Content.ReadFromJsonAsync<JiraIssueResponse>(CamelCaseOptions);
        if (issue?.Fields?.Status?.Name == null) return null;

        var status = issue.Fields.Status.Name;
        var isDone = status.Equals("Done", StringComparison.OrdinalIgnoreCase) ||
                     status.Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
                     status.Equals("Resolved", StringComparison.OrdinalIgnoreCase);

        return new ExternalIssueStatus(externalId, status, isDone);
    }

    public async Task<bool> CloseIssueAsync(TrackerConfig config, string externalId)
    {
        var cfg = ParseConfig<JiraTrackerConfig>(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg);

        var transitionsResponse = await http.GetAsync(
            $"{cfg.BaseUrl.TrimEnd('/')}/rest/api/3/issue/{externalId}/transitions");
        if (!transitionsResponse.IsSuccessStatusCode) return false;

        var transitions = await transitionsResponse.Content.ReadFromJsonAsync<JiraTransitionsResponse>(CamelCaseOptions);
        var doneTransition = transitions?.Transitions?.FirstOrDefault(t => t.To?.StatusCategory?.Key == "done");
        if (doneTransition == null) return false;

        var body = new { transition = new { id = doneTransition.Id } };
        var response = await http.PostAsJsonAsync(
            $"{cfg.BaseUrl.TrimEnd('/')}/rest/api/3/issue/{externalId}/transitions", body, CamelCaseOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> TestConnectionAsync(TrackerConfig config)
    {
        var cfg = ParseConfig<JiraTrackerConfig>(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg);

        var response = await http.GetAsync($"{cfg.BaseUrl.TrimEnd('/')}/rest/api/3/project/{cfg.ProjectKey}");
        return response.IsSuccessStatusCode;
    }

    private HttpClient CreateHttpClient(JiraTrackerConfig cfg)
    {
        var http = CreateClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{cfg.Email}:{cfg.ApiToken}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    private record JiraTrackerConfig(string BaseUrl, string Email, string ApiToken, string ProjectKey);
    private record JiraCreateResponse([property: JsonPropertyName("key")] string Key);
    private record JiraIssueResponse([property: JsonPropertyName("fields")] JiraFields? Fields);
    private record JiraFields([property: JsonPropertyName("status")] JiraStatus? Status);
    private record JiraStatus([property: JsonPropertyName("name")] string? Name);
    private record JiraTransitionsResponse([property: JsonPropertyName("transitions")] List<JiraTransition>? Transitions);
    private record JiraTransition(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("to")] JiraTransitionTo? To);
    private record JiraTransitionTo([property: JsonPropertyName("statusCategory")] JiraStatusCategory? StatusCategory);
    private record JiraStatusCategory([property: JsonPropertyName("key")] string? Key);
}
