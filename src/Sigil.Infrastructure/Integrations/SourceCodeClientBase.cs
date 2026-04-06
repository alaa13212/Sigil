using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceCode;

namespace Sigil.Infrastructure.Integrations;

internal abstract class SourceCodeClientBase(IHttpClientFactory httpClientFactory)
{
    protected HttpClient CreateClient() => httpClientFactory.CreateClient();

    public async Task<SourceContextLines?> GetSourceContextAsync(
        ResolvedRepository repo, string filePath, int lineNumber, string? commitSha, int contextLines = 5)
    {
        var result = await GetSourceContextCoreAsync(repo, filePath, lineNumber, commitSha, contextLines);

        // If search failed with a specific commit SHA, retry with default branch
        if (result == null && commitSha != null)
            result = await GetSourceContextCoreAsync(repo, filePath, lineNumber, null, contextLines);

        return result;
    }

    protected abstract Task<SourceContextLines?> GetSourceContextCoreAsync(
        ResolvedRepository repo, string filePath, int lineNumber, string? commitSha, int contextLines);

    public abstract Task<CommitInfo?> GetCommitAsync(ResolvedRepository repo, string commitSha);
}
