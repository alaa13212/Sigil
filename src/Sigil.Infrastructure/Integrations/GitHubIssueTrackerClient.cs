using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.IssueTrackers;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Integrations;

internal class GitHubIssueTrackerClient(IHttpClientFactory httpClientFactory)
    : TrackerClientBase(httpClientFactory), IIssueTrackerClient
{
    public TrackerType TrackerType => TrackerType.GitHub;

    public async Task<ExternalIssueResult> CreateIssueAsync(TrackerConfig config, CreateExternalIssueRequest request)
    {
        var cfg = ParseConfig<GitHubTrackerConfig>(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.Pat);

        var body = new { title = request.Title, body = $"{request.Description}\n\n---\n[View in Sigil]({request.IssueUrl})" };
        var response = await http.PostAsJsonAsync($"https://api.github.com/repos/{cfg.Owner}/{cfg.Repo}/issues", body);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<GitHubIssueResponse>(CamelCaseOptions);
        return new ExternalIssueResult(result!.Number.ToString(), result.HtmlUrl, "open");
    }

    public async Task<ExternalIssueStatus?> GetStatusAsync(TrackerConfig config, string externalId)
    {
        var cfg = ParseConfig<GitHubTrackerConfig>(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.Pat);

        var response = await http.GetAsync($"https://api.github.com/repos/{cfg.Owner}/{cfg.Repo}/issues/{externalId}");
        if (!response.IsSuccessStatusCode) return null;

        var issue = await response.Content.ReadFromJsonAsync<GitHubIssueResponse>(CamelCaseOptions);
        if (issue == null) return null;

        return new ExternalIssueStatus(externalId, issue.State, issue.State == "closed");
    }

    public async Task<bool> CloseIssueAsync(TrackerConfig config, string externalId)
    {
        var cfg = ParseConfig<GitHubTrackerConfig>(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.Pat);

        var body = new { state = "closed" };
        var response = await http.PatchAsJsonAsync(
            $"https://api.github.com/repos/{cfg.Owner}/{cfg.Repo}/issues/{externalId}", body, CamelCaseOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> TestConnectionAsync(TrackerConfig config)
    {
        var cfg = ParseConfig<GitHubTrackerConfig>(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.Pat);

        var response = await http.GetAsync($"https://api.github.com/repos/{cfg.Owner}/{cfg.Repo}");
        return response.IsSuccessStatusCode;
    }

    private HttpClient CreateHttpClient(string pat)
    {
        var http = CreateClient();
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Sigil", "1.0"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        return http;
    }

    private record GitHubTrackerConfig(string Owner, string Repo, string Pat);
    private record GitHubIssueResponse(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("state")] string State);
}
