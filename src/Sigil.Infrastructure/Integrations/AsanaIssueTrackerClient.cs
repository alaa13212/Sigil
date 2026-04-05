using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.IssueTrackers;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Integrations;

internal class AsanaIssueTrackerClient : IIssueTrackerClient
{
    public TrackerType TrackerType => TrackerType.Asana;

    public async Task<ExternalIssueResult> CreateIssueAsync(TrackerConfig config, CreateExternalIssueRequest request)
    {
        var cfg = ParseConfig(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.Pat);

        var body = new
        {
            data = new
            {
                name = request.Title,
                notes = $"{request.Description}\n\nSigil: {request.IssueUrl}",
                projects = new[] { cfg.ProjectGid }
            }
        };

        var response = await http.PostAsJsonAsync("https://app.asana.com/api/1.0/tasks", body);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AsanaTaskResponse>(JsonOptions);
        var task = result?.Data ?? throw new InvalidOperationException("Asana task creation failed.");
        var taskUrl = $"https://app.asana.com/0/{cfg.ProjectGid}/{task.Gid}";

        return new ExternalIssueResult(task.Gid, taskUrl, task.Completed ? "completed" : "open");
    }

    public async Task<ExternalIssueStatus?> GetStatusAsync(TrackerConfig config, string externalId)
    {
        var cfg = ParseConfig(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.Pat);

        var response = await http.GetAsync($"https://app.asana.com/api/1.0/tasks/{externalId}?opt_fields=gid,completed");
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<AsanaTaskResponse>(JsonOptions);
        if (result?.Data == null) return null;

        var status = result.Data.Completed ? "completed" : "open";
        return new ExternalIssueStatus(externalId, status, result.Data.Completed);
    }

    public async Task<bool> CloseIssueAsync(TrackerConfig config, string externalId)
    {
        var cfg = ParseConfig(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.Pat);

        var body = new { data = new { completed = true } };
        var response = await http.PutAsJsonAsync($"https://app.asana.com/api/1.0/tasks/{externalId}", body, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> TestConnectionAsync(TrackerConfig config)
    {
        var cfg = ParseConfig(config.DecryptedConfigJson);
        using var http = CreateHttpClient(cfg.Pat);

        var response = await http.GetAsync($"https://app.asana.com/api/1.0/projects/{cfg.ProjectGid}");
        return response.IsSuccessStatusCode;
    }

    private static HttpClient CreateHttpClient(string pat)
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    private static AsanaTrackerConfig ParseConfig(string json) =>
        JsonSerializer.Deserialize<AsanaTrackerConfig>(json, JsonOptions)
        ?? throw new InvalidOperationException("Invalid Asana tracker config.");

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private record AsanaTrackerConfig(string Pat, string ProjectGid);
    private record AsanaTaskResponse([property: JsonPropertyName("data")] AsanaTask? Data);
    private record AsanaTask([property: JsonPropertyName("gid")] string Gid, [property: JsonPropertyName("completed")] bool Completed);
}
