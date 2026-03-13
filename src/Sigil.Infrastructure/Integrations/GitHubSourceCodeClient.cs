using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceCode;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Integrations;

internal class GitHubSourceCodeClient : ISourceCodeClient
{
    public ProviderType ProviderType => ProviderType.GitHub;

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
        var baseApi = GetBaseApiUrl(repo.BaseUrl);
        using var http = CreateHttpClient(repo.AccessToken);

        var resolvedPath = await ResolvePathAsync(http, baseApi, repo, filePath, commitSha);
        if (resolvedPath == null) return null;

        var url = $"{baseApi}/repos/{repo.RepositoryOwner}/{repo.RepositoryName}/contents/{resolvedPath}";
        if (commitSha != null) url += $"?ref={commitSha}";

        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadFromJsonAsync<GitHubFileContent>();
        if (content?.Content == null) return null;

        var decoded = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(content.Content.Replace("\n", "")));

        return new SourceContextLines(
            SourceCodeClientHelper.ExtractContext(decoded, lineNumber, contextLines),
            resolvedPath);
    }

    public async Task<CommitInfo?> GetCommitAsync(ResolvedRepository repo, string commitSha)
    {
        var baseApi = GetBaseApiUrl(repo.BaseUrl);
        var url = $"{baseApi}/repos/{repo.RepositoryOwner}/{repo.RepositoryName}/commits/{commitSha}";

        using var http = CreateHttpClient(repo.AccessToken);
        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var commit = await response.Content.ReadFromJsonAsync<GitHubCommit>();
        if (commit == null) return null;

        var commitUrl = $"{repo.BaseUrl.TrimEnd('/')}/{repo.RepositoryOwner}/{repo.RepositoryName}/commit/{commitSha}";
        return new CommitInfo(
            commitSha,
            commit.Commit?.Message,
            commit.Commit?.Author?.Name,
            commit.Commit?.Author?.Email,
            commit.Commit?.Author?.Date,
            commitUrl);
    }

    /// <summary>
    /// Resolves a Sentry-provided file path (which may be a bare filename, sub-project relative path,
    /// or absolute path) to the actual path within the repository using the Git tree API.
    /// </summary>
    private static async Task<string?> ResolvePathAsync(
        HttpClient http, string baseApi, ResolvedRepository repo, string filePath, string? commitSha)
    {
        var candidate = SourceCodeClientHelper.NormalizePath(filePath);

        // 1. Try exact path first
        var directUrl = $"{baseApi}/repos/{repo.RepositoryOwner}/{repo.RepositoryName}/contents/{candidate}";
        if (commitSha != null) directUrl += $"?ref={commitSha}";
        var check = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, directUrl));
        if (check.IsSuccessStatusCode) return candidate;

        // 2. Fetch the full tree and find the best matching file
        var @ref = commitSha ?? repo.DefaultBranch ?? "master";
        var treeUrl = $"{baseApi}/repos/{repo.RepositoryOwner}/{repo.RepositoryName}/git/trees/{@ref}?recursive=1";
        var treeResponse = await http.GetAsync(treeUrl);
        if (!treeResponse.IsSuccessStatusCode) return null;

        var tree = await treeResponse.Content.ReadFromJsonAsync<GitHubTree>();
        var blobs = tree?.Items?.Where(i => i.Type == "blob" && i.Path != null).ToList();
        if (blobs == null || blobs.Count == 0) return null;

        return SourceCodeClientHelper.FindBestMatch(blobs.Select(b => b.Path!), candidate);
    }

    private static string GetBaseApiUrl(string baseUrl) =>
        baseUrl.TrimEnd('/') == "https://github.com"
            ? "https://api.github.com"
            : $"{baseUrl.TrimEnd('/')}/api/v3";

    private static HttpClient CreateHttpClient(string token)
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        http.DefaultRequestHeaders.Add("User-Agent", "Sigil/1.0");
        http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return http;
    }

    private record GitHubFileContent([property: JsonPropertyName("content")] string? Content);

    private record GitHubCommit([property: JsonPropertyName("commit")] GitHubCommitDetail? Commit);

    private record GitHubCommitDetail(
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("author")] GitHubCommitAuthor? Author);

    private record GitHubCommitAuthor(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("date")] DateTime? Date);

    private record GitHubTree(
        [property: JsonPropertyName("tree")] List<GitHubTreeItem>? Items);

    private record GitHubTreeItem(
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("type")] string? Type);
}
