using Sigil.Domain.Entities;

namespace Sigil.Application.Models.SourceCode;

public record SourceCodeProviderResponse(
    int Id,
    string Name,
    ProviderType Type,
    string BaseUrl,
    DateTime CreatedAt);

public record CreateProviderRequest(
    string Name,
    ProviderType Type,
    string BaseUrl,
    string AccessToken);

public record ProjectRepositoryResponse(
    int Id,
    int ProjectId,
    int ProviderId,
    string ProviderName,
    ProviderType ProviderType,
    string RepositoryOwner,
    string RepositoryName,
    string? DefaultBranch);

public record LinkRepositoryRequest(
    int ProviderId,
    string RepositoryOwner,
    string RepositoryName,
    string? DefaultBranch);

public record SourceContextResponse(
    string Filename,
    int TargetLine,
    List<SourceLine> Lines,
    string? FileUrl = null);

public record SourceLine(
    int LineNumber,
    string Content);

public record CommitInfo(
    string Sha,
    string? Message,
    string? AuthorName,
    string? AuthorEmail,
    DateTime? CommittedAt,
    string? Url);

/// <summary>
/// Resolved repository with decrypted access token, ready to be used by source code clients.
/// </summary>
public record ResolvedRepository(
    string RepositoryOwner,
    string RepositoryName,
    string? DefaultBranch,
    string BaseUrl,
    string AccessToken,
    ProviderType ProviderType);
