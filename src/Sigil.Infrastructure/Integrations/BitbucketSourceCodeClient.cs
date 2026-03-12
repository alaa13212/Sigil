using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceCode;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Integrations;

internal class BitbucketSourceCodeClient : ISourceCodeClient
{
    public ProviderType ProviderType => ProviderType.Bitbucket;

    public async Task<SourceContextLines?> GetSourceContextAsync(
        ResolvedRepository repo,
        string filePath,
        int lineNumber,
        string? commitSha,
        int contextLines = 5)
    {
        var @ref = commitSha ?? repo.DefaultBranch ?? "master";

        using var http = CreateHttpClient(repo.AccessToken);

        var resolvedPath = await ResolvePathAsync(http, repo, filePath, @ref);
        if (resolvedPath == null) return null;

        var url = $"https://api.bitbucket.org/2.0/repositories/{repo.RepositoryOwner}/{repo.RepositoryName}/src/{@ref}/{resolvedPath}";
        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var content = await response.Content.ReadAsStringAsync();
        return new SourceContextLines(
            SourceCodeClientHelper.ExtractContext(content, lineNumber, contextLines),
            resolvedPath);
    }

    public async Task<CommitInfo?> GetCommitAsync(ResolvedRepository repo, string commitSha)
    {
        var url = $"https://api.bitbucket.org/2.0/repositories/{repo.RepositoryOwner}/{repo.RepositoryName}/commit/{commitSha}";

        using var http = CreateHttpClient(repo.AccessToken);
        var response = await http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var commit = await response.Content.ReadFromJsonAsync<BitbucketCommit>();
        if (commit == null) return null;

        var commitUrl = $"https://bitbucket.org/{repo.RepositoryOwner}/{repo.RepositoryName}/commits/{commitSha}";
        return new CommitInfo(
            commitSha,
            commit.Message,
            commit.Author?.User?.DisplayName ?? commit.Author?.Raw,
            null,
            commit.Date,
            commitUrl);
    }

    private static async Task<string?> ResolvePathAsync(
        HttpClient http, ResolvedRepository repo, string filePath, string @ref)
    {
        var candidate = SourceCodeClientHelper.NormalizePath(filePath);

        // 1. Try exact path first
        var directUrl = $"https://api.bitbucket.org/2.0/repositories/{repo.RepositoryOwner}/{repo.RepositoryName}/src/{@ref}/{candidate}";
        var check = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, directUrl));
        if (check.IsSuccessStatusCode) return candidate;

        // 2. List all files recursively (paginated) and find the best match
        var allPaths = new List<string>();
        var listUrl = (string?)$"https://api.bitbucket.org/2.0/repositories/{repo.RepositoryOwner}/{repo.RepositoryName}/src/{@ref}/?pagelen=100&recursive=true&fields=values.path,values.type,next";

        while (listUrl != null)
        {
            var listResponse = await http.GetAsync(listUrl);
            if (!listResponse.IsSuccessStatusCode) break;

            var page = await listResponse.Content.ReadFromJsonAsync<BitbucketPage>();
            if (page?.Values != null)
                allPaths.AddRange(page.Values.Where(v => v.Type == "commit_file" && v.Path != null).Select(v => v.Path!));

            listUrl = page?.Next;
        }

        return allPaths.Count == 0 ? null : SourceCodeClientHelper.FindBestMatch(allPaths, candidate);
    }

    // Bitbucket uses App Passwords in "username:app_password" format
    private static HttpClient CreateHttpClient(string token)
    {
        var http = new HttpClient();
        var encoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(token));
        http.DefaultRequestHeaders.Add("Authorization", $"Basic {encoded}");
        return http;
    }

    private record BitbucketCommit(
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("date")] DateTime? Date,
        [property: JsonPropertyName("author")] BitbucketAuthor? Author);

    private record BitbucketAuthor(
        [property: JsonPropertyName("raw")] string? Raw,
        [property: JsonPropertyName("user")] BitbucketUser? User);

    private record BitbucketUser(
        [property: JsonPropertyName("display_name")] string? DisplayName);

    private record BitbucketPage(
        [property: JsonPropertyName("values")] List<BitbucketFileEntry>? Values,
        [property: JsonPropertyName("next")] string? Next);

    private record BitbucketFileEntry(
        [property: JsonPropertyName("path")] string? Path,
        [property: JsonPropertyName("type")] string? Type);
}
