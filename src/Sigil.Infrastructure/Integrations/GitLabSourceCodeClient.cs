using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Web;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceCode;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Integrations;

internal class GitLabSourceCodeClient : ISourceCodeClient
{
    public ProviderType ProviderType => ProviderType.GitLab;

    public async Task<SourceContextLines?> GetSourceContextAsync(
        ResolvedRepository repo,
        string filePath,
        int lineNumber,
        string? commitSha,
        int contextLines = 5)
    {
        var result = await GetSourceContextCoreAsync(repo, filePath, lineNumber, commitSha, contextLines);

        // If search failed with a specific commit SHA, retry with default branch
        if (result == null && commitSha != null)
            result = await GetSourceContextCoreAsync(repo, filePath, lineNumber, null, contextLines);

        return result;
    }

    private async Task<SourceContextLines?> GetSourceContextCoreAsync(
        ResolvedRepository repo,
        string filePath,
        int lineNumber,
        string? commitSha,
        int contextLines)
    {
        var baseApi = $"{repo.BaseUrl.TrimEnd('/')}/api/v4";
        var projectPath = HttpUtility.UrlEncode($"{repo.RepositoryOwner}/{repo.RepositoryName}");
        var @ref = commitSha ?? repo.DefaultBranch ?? "master";

        using var http = CreateHttpClient(repo.AccessToken);

        var resolvedPath = await ResolvePathAsync(http, baseApi, projectPath, filePath, @ref);
        if (resolvedPath == null) return null;

        var encodedFilePath = HttpUtility.UrlEncode(resolvedPath);
        var url = $"{baseApi}/projects/{projectPath}/repository/files/{encodedFilePath}/raw?ref={@ref}";

        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsStringAsync();
        return new SourceContextLines(
            SourceCodeClientHelper.ExtractContext(content, lineNumber, contextLines),
            resolvedPath);
    }

    public async Task<CommitInfo?> GetCommitAsync(ResolvedRepository repo, string commitSha)
    {
        var baseApi = $"{repo.BaseUrl.TrimEnd('/')}/api/v4";
        var projectPath = HttpUtility.UrlEncode($"{repo.RepositoryOwner}/{repo.RepositoryName}");
        var url = $"{baseApi}/projects/{projectPath}/repository/commits/{commitSha}";

        using var http = CreateHttpClient(repo.AccessToken);
        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var commit = await response.Content.ReadFromJsonAsync<GitLabCommit>();
        if (commit == null) return null;

        var commitUrl = $"{repo.BaseUrl.TrimEnd('/')}/{repo.RepositoryOwner}/{repo.RepositoryName}/-/commit/{commitSha}";
        return new CommitInfo(
            commitSha,
            commit.Title,
            commit.AuthorName,
            commit.AuthorEmail,
            commit.CommittedDate,
            commitUrl);
    }

    private static async Task<string?> ResolvePathAsync(
        HttpClient http, string baseApi, string projectPath, string filePath, string @ref)
    {
        var candidate = SourceCodeClientHelper.NormalizePath(filePath);

        // 1. Try exact path first
        var encodedCandidate = HttpUtility.UrlEncode(candidate);
        var directUrl = $"{baseApi}/projects/{projectPath}/repository/files/{encodedCandidate}?ref={@ref}";
        var check = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, directUrl));
        if (check.IsSuccessStatusCode) return candidate;

        // 2. Fetch the full tree (paginated) and find the best matching file
        var allPaths = new List<string>();
        var treeUrl = (string?)$"{baseApi}/projects/{projectPath}/repository/tree?recursive=true&ref={@ref}&per_page=100";

        while (treeUrl != null)
        {
            var treeResponse = await http.GetAsync(treeUrl);
            if (!treeResponse.IsSuccessStatusCode) break;

            var items = await treeResponse.Content.ReadFromJsonAsync<List<GitLabTreeItem>>();
            if (items != null)
                allPaths.AddRange(items.Where(i => i.Type == "blob" && i.Path != null).Select(i => i.Path!));

            // GitLab returns next page URL in the Link header
            treeUrl = ParseNextLink(treeResponse);
        }

        return allPaths.Count == 0 ? null : SourceCodeClientHelper.FindBestMatch(allPaths, candidate);
    }

    private static string? ParseNextLink(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values)) return null;
        foreach (var value in values)
        {
            foreach (var part in value.Split(','))
            {
                var segments = part.Trim().Split(';');
                if (segments.Length == 2 && segments[1].Trim() == "rel=\"next\"")
                    return segments[0].Trim().Trim('<', '>');
            }
        }
        return null;
    }

    private static HttpClient CreateHttpClient(string token)
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.Add("PRIVATE-TOKEN", token);
        return http;
    }

    private record GitLabCommit(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("author_name")] string? AuthorName,
        [property: JsonPropertyName("author_email")] string? AuthorEmail,
        [property: JsonPropertyName("committed_date")] DateTime? CommittedDate);

    private record GitLabTreeItem(
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("type")] string? Type);
}
