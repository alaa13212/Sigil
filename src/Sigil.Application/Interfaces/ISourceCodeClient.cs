using Sigil.Application.Models.SourceCode;
using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface ISourceCodeClient
{
    ProviderType ProviderType { get; }

    Task<SourceContextLines?> GetSourceContextAsync(
        ResolvedRepository repo,
        string filePath,
        int lineNumber,
        string? commitSha,
        int contextLines = 5);

    Task<CommitInfo?> GetCommitAsync(ResolvedRepository repo, string commitSha);
}

public record SourceContextLines(List<SourceLine> Lines, string ResolvedPath);
