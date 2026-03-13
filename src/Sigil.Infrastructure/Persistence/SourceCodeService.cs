using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceCode;
using Sigil.Domain;
using Sigil.Domain.Entities;
using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Persistence;

internal class SourceCodeService(
    SigilDbContext dbContext,
    IDateTime dateTime,
    TokenEncryptionService tokenEncryption,
    IEnumerable<ISourceCodeClient> clients,
    IMemoryCache cache) : ISourceCodeService
{
    public async Task<SourceCodeProviderResponse> AddProviderAsync(CreateProviderRequest request, Guid createdByUserId)
    {
        var provider = new SourceCodeProvider
        {
            Name = request.Name,
            Type = request.Type,
            BaseUrl = request.BaseUrl.TrimEnd('/'),
            EncryptedAccessToken = tokenEncryption.Encrypt(request.AccessToken),
            CreatedAt = dateTime.UtcNow,
            CreatedByUserId = createdByUserId
        };

        dbContext.SourceCodeProviders.Add(provider);
        await dbContext.SaveChangesAsync();
        return ToProviderResponse(provider);
    }

    public async Task<List<SourceCodeProviderResponse>> GetProvidersAsync()
    {
        var providers = await dbContext.SourceCodeProviders
            .OrderBy(p => p.Name)
            .ToListAsync();

        return providers.Select(ToProviderResponse).ToList();
    }

    public async Task<bool> DeleteProviderAsync(int id)
    {
        var inUse = await dbContext.ProjectRepositories.AnyAsync(r => r.ProviderId == id);
        if (inUse) return false;

        var deleted = await dbContext.SourceCodeProviders.Where(p => p.Id == id).ExecuteDeleteAsync();
        return deleted > 0;
    }

    public async Task<bool> TestConnectionAsync(int providerId, string testOwner, string testRepo)
    {
        var provider = await dbContext.SourceCodeProviders.FirstOrDefaultAsync(p => p.Id == providerId);
        if (provider == null) return false;

        var repo = new ResolvedRepository(testOwner, testRepo, null, provider.BaseUrl,
            tokenEncryption.Decrypt(provider.EncryptedAccessToken), provider.Type);

        var client = GetClient(provider.Type);
        try
        {
            var commit = await client.GetCommitAsync(repo, "HEAD");
            return commit != null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ProjectRepositoryResponse> LinkRepositoryAsync(int projectId, LinkRepositoryRequest request)
    {
        var provider = await dbContext.SourceCodeProviders.FirstOrDefaultAsync(p => p.Id == request.ProviderId)
            ?? throw new InvalidOperationException($"Provider {request.ProviderId} not found.");

        var repo = new ProjectRepository
        {
            ProjectId = projectId,
            ProviderId = request.ProviderId,
            RepositoryOwner = request.RepositoryOwner,
            RepositoryName = request.RepositoryName,
            DefaultBranch = request.DefaultBranch
        };

        dbContext.ProjectRepositories.Add(repo);
        await dbContext.SaveChangesAsync();
        return ToRepositoryResponse(repo, provider);
    }

    public async Task<List<ProjectRepositoryResponse>> GetRepositoriesAsync(int projectId)
    {
        var repos = await dbContext.ProjectRepositories
            .Include(r => r.Provider)
            .Where(r => r.ProjectId == projectId)
            .ToListAsync();

        return repos.Select(r => ToRepositoryResponse(r, r.Provider!)).ToList();
    }

    public async Task<bool> UnlinkRepositoryAsync(int projectId, int repositoryId)
    {
        var deleted = await dbContext.ProjectRepositories
            .Where(r => r.Id == repositoryId && r.ProjectId == projectId)
            .ExecuteDeleteAsync();
        return deleted > 0;
    }

    public async Task<SourceContextResponse?> GetSourceContextForEventAsync(long eventId, string filename, int line)
    {
        var eventInfo = await dbContext.Events
            .Where(e => e.Id == eventId)
            .Select(e => new
            {
                e.ProjectId,
                CommitSha  = e.Release != null ? e.Release.CommitSha : null,
                ReleaseTag = e.Release != null ? e.Release.RawName   : null
            })
            .FirstOrDefaultAsync();

        if (eventInfo == null) return null;

        // Use commit SHA when available; fall back to release name as a git tag
        var @ref = eventInfo.CommitSha ?? CleanReleaseTag(eventInfo.ReleaseTag);
        return await FetchSourceContextAsync(eventInfo.ProjectId, filename, line, @ref);
    }

    private string? CleanReleaseTag(string? releaseTag)
    {
        if(releaseTag == null)
            return null;
        return releaseTag.Contains('@')? releaseTag.Split('@').Last() : releaseTag;
    }

    private async Task<SourceContextResponse?> FetchSourceContextAsync(
        int projectId, string filename, int line, string? commitSha)
    {
        var cacheKey = $"src-ctx:{projectId}:{filename}:{line}:{commitSha ?? "HEAD"}";
        if (cache.TryGetValue(cacheKey, out SourceContextResponse? cached))
            return cached;

        var resolved = await ResolveRepositoryAsync(projectId);
        if (resolved == null) return null;

        var client = GetClient(resolved.ProviderType);
        var context = await client.GetSourceContextAsync(resolved, filename, line, commitSha);
        if (context == null) return null;

        var @ref = commitSha ?? resolved.DefaultBranch ?? "master";
        var fileUrl = BuildFileUrl(resolved, context.ResolvedPath, @ref, line);
        var result = new SourceContextResponse(filename, line, context.Lines, fileUrl);

        var expiry = commitSha != null ? TimeSpan.FromDays(365) : TimeSpan.FromMinutes(10);
        cache.Set(cacheKey, result, expiry);

        return result;
    }

    public async Task<CommitInfo?> GetCommitInfoAsync(int projectId, string commitSha)
    {
        var cacheKey = $"commit:{projectId}:{commitSha}";
        if (cache.TryGetValue(cacheKey, out CommitInfo? cached))
            return cached;

        var resolved = await ResolveRepositoryAsync(projectId);
        if (resolved == null)
            return null;

        var client = GetClient(resolved.ProviderType);
        var info = await client.GetCommitAsync(resolved, commitSha);
        if (info != null)
            cache.Set(cacheKey, info, TimeSpan.FromDays(365)); // commits are immutable

        return info;
    }

    private async Task<ResolvedRepository?> ResolveRepositoryAsync(int projectId)
    {
        var repo = await dbContext.ProjectRepositories
            .Include(r => r.Provider)
            .Where(r => r.ProjectId == projectId)
            .FirstOrDefaultAsync();

        if (repo?.Provider == null) return null;

        return new ResolvedRepository(
            repo.RepositoryOwner,
            repo.RepositoryName,
            repo.DefaultBranch,
            repo.Provider.BaseUrl,
            tokenEncryption.Decrypt(repo.Provider.EncryptedAccessToken),
            repo.Provider.Type);
    }

    private ISourceCodeClient GetClient(ProviderType type)
    {
        return clients.FirstOrDefault(c => c.ProviderType == type)
            ?? throw new InvalidOperationException($"No source code client registered for {type}.");
    }

    private static string? BuildFileUrl(ResolvedRepository repo, string resolvedPath, string @ref, int line) =>
        repo.ProviderType switch
        {
            ProviderType.GitHub    => $"{repo.BaseUrl.TrimEnd('/')}/{repo.RepositoryOwner}/{repo.RepositoryName}/blob/{@ref}/{resolvedPath}#L{line}",
            ProviderType.GitLab    => $"{repo.BaseUrl.TrimEnd('/')}/{repo.RepositoryOwner}/{repo.RepositoryName}/-/blob/{@ref}/{resolvedPath}#L{line}",
            ProviderType.Bitbucket => $"{repo.BaseUrl.TrimEnd('/')}/{repo.RepositoryOwner}/{repo.RepositoryName}/src/{@ref}/{resolvedPath}#lines-{line}",
            _                      => null
        };

    private static SourceCodeProviderResponse ToProviderResponse(SourceCodeProvider p) =>
        new(p.Id, p.Name, p.Type, p.BaseUrl, p.CreatedAt);

    private static ProjectRepositoryResponse ToRepositoryResponse(ProjectRepository r, SourceCodeProvider provider) =>
        new(r.Id, r.ProjectId, r.ProviderId, provider.Name, provider.Type,
            r.RepositoryOwner, r.RepositoryName, r.DefaultBranch);
}
