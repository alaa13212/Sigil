using Sigil.Application.Models.SourceCode;

namespace Sigil.Application.Interfaces;

public interface ISourceCodeService
{
    // Provider management (admin-scoped)
    Task<SourceCodeProviderResponse> AddProviderAsync(CreateProviderRequest request, Guid createdByUserId);
    Task<List<SourceCodeProviderResponse>> GetProvidersAsync();
    Task<bool> DeleteProviderAsync(int id);
    Task<bool> TestConnectionAsync(int providerId, string testOwner, string testRepo);

    // Repository linking (project-scoped)
    Task<ProjectRepositoryResponse> LinkRepositoryAsync(int projectId, LinkRepositoryRequest request);
    Task<List<ProjectRepositoryResponse>> GetRepositoriesAsync(int projectId);
    Task<bool> UnlinkRepositoryAsync(int projectId, int repositoryId);

    // Source context & commit info (fetched from VCS API)
    // eventId overload: server resolves projectId + commitSha from the event's release automatically
    Task<SourceContextResponse?> GetSourceContextForEventAsync(long eventId, string filename, int line);
    Task<CommitInfo?> GetCommitInfoAsync(int projectId, string commitSha);
}
