using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceCode;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class SourceCodeServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2026, 3, 12, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static TokenEncryptionService CreateEncryption()
        => new TokenEncryptionService(new EphemeralDataProtectionProvider());

    private static SourceCodeService CreateService(
        SigilDbContext context,
        TokenEncryptionService? encryption = null,
        ISourceCodeClient? mockClient = null)
    {
        encryption ??= CreateEncryption();
        var clients = mockClient != null
            ? (IEnumerable<ISourceCodeClient>)[mockClient]
            : Array.Empty<ISourceCodeClient>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new SourceCodeService(context, StubDateTime(), encryption, clients, cache);
    }

    private static async Task<SourceCodeProvider> CreateProviderAsync(
        SigilDbContext context,
        TokenEncryptionService encryption,
        string? name = null,
        ProviderType type = ProviderType.GitHub)
    {
        var provider = new SourceCodeProvider
        {
            Name = name ?? $"Provider-{Guid.NewGuid():N}",
            Type = type,
            BaseUrl = "https://github.com",
            EncryptedAccessToken = encryption.Encrypt("raw-token"),
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = Guid.NewGuid(),
        };
        context.SourceCodeProviders.Add(provider);
        await context.SaveChangesAsync();
        return provider;
    }

    // -----------------------------------------------------------------------
    // AddProviderAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddProvider_PersistsAndReturnsResponse()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var encryption = CreateEncryption();
        var service = CreateService(ctx, encryption);
        const string rawToken = "ghp_mysecrettoken";

        var request = new CreateProviderRequest("My GitHub", ProviderType.GitHub, "https://github.com/", rawToken);
        var response = await service.AddProviderAsync(request, user.Id);

        response.Id.Should().BeGreaterThan(0);
        response.Name.Should().Be("My GitHub");
        response.Type.Should().Be(ProviderType.GitHub);
        response.BaseUrl.Should().Be("https://github.com"); // trailing slash trimmed

        // Verify the token is stored encrypted (not as the original value)
        await using var verifyCtx = Ctx();
        var inDb = await verifyCtx.SourceCodeProviders.FindAsync(response.Id);
        inDb.Should().NotBeNull();
        inDb!.EncryptedAccessToken.Should().NotBe(rawToken);
        // The stored value should decrypt back to the original using the same key
        encryption.Decrypt(inDb.EncryptedAccessToken).Should().Be(rawToken);
    }

    // -----------------------------------------------------------------------
    // GetProvidersAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetProviders_ReturnsAllOrderedByName()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var encryption = CreateEncryption();
        var service = CreateService(ctx, encryption);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        await service.AddProviderAsync(
            new CreateProviderRequest($"Z-Provider-{suffix}", ProviderType.GitLab, "https://gitlab.com", "tok"), user.Id);
        await service.AddProviderAsync(
            new CreateProviderRequest($"A-Provider-{suffix}", ProviderType.GitHub, "https://github.com", "tok"), user.Id);

        var all = await service.GetProvidersAsync();
        var relevant = all.Where(p => p.Name.EndsWith(suffix)).Select(p => p.Name).ToList();

        relevant.Should().HaveCount(2);
        relevant.Should().BeInAscendingOrder();
    }

    // -----------------------------------------------------------------------
    // DeleteProviderAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeleteProvider_WhenUnlinked_Succeeds()
    {
        await using var ctx = Ctx();
        var encryption = CreateEncryption();
        var provider = await CreateProviderAsync(ctx, encryption);
        var service = CreateService(ctx, encryption);

        var result = await service.DeleteProviderAsync(provider.Id);

        result.Should().BeTrue();

        await using var verifyCtx = Ctx();
        var inDb = await verifyCtx.SourceCodeProviders.FindAsync(provider.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProvider_WhenLinked_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var encryption = CreateEncryption();
        var provider = await CreateProviderAsync(ctx, encryption);
        var project = await TestHelper.CreateProjectAsync(ctx);

        // Link a repo to this provider
        var repo = new ProjectRepository
        {
            ProjectId = project.Id,
            ProviderId = provider.Id,
            RepositoryOwner = "my-org",
            RepositoryName = "my-repo",
        };
        ctx.ProjectRepositories.Add(repo);
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx, encryption);
        var result = await service.DeleteProviderAsync(provider.Id);

        result.Should().BeFalse();

        await using var verifyCtx = Ctx();
        var stillInDb = await verifyCtx.SourceCodeProviders.FindAsync(provider.Id);
        stillInDb.Should().NotBeNull();
    }

    // -----------------------------------------------------------------------
    // LinkRepositoryAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LinkRepository_PersistsRepo()
    {
        await using var ctx = Ctx();
        var encryption = CreateEncryption();
        var provider = await CreateProviderAsync(ctx, encryption);
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx, encryption);

        var request = new LinkRepositoryRequest(provider.Id, "acme-corp", "backend-api", "main");
        var response = await service.LinkRepositoryAsync(project.Id, request);

        response.Id.Should().BeGreaterThan(0);
        response.ProjectId.Should().Be(project.Id);
        response.ProviderId.Should().Be(provider.Id);
        response.RepositoryOwner.Should().Be("acme-corp");
        response.RepositoryName.Should().Be("backend-api");
        response.DefaultBranch.Should().Be("main");
    }

    // -----------------------------------------------------------------------
    // GetRepositoriesAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetRepositories_ReturnsLinkedRepos()
    {
        await using var ctx = Ctx();
        var encryption = CreateEncryption();
        var provider = await CreateProviderAsync(ctx, encryption);
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx, encryption);

        await service.LinkRepositoryAsync(project.Id, new LinkRepositoryRequest(provider.Id, "org", "repo-one", null));
        await service.LinkRepositoryAsync(project.Id, new LinkRepositoryRequest(provider.Id, "org", "repo-two", null));

        var repos = await service.GetRepositoriesAsync(project.Id);

        repos.Should().HaveCount(2);
        repos.Select(r => r.RepositoryName).Should().Contain(["repo-one", "repo-two"]);
    }

    // -----------------------------------------------------------------------
    // UnlinkRepositoryAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnlinkRepository_RemovesRepo()
    {
        await using var ctx = Ctx();
        var encryption = CreateEncryption();
        var provider = await CreateProviderAsync(ctx, encryption);
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx, encryption);

        var linked = await service.LinkRepositoryAsync(project.Id,
            new LinkRepositoryRequest(provider.Id, "org", "my-repo", "main"));

        var result = await service.UnlinkRepositoryAsync(project.Id, linked.Id);

        result.Should().BeTrue();

        var repos = await service.GetRepositoriesAsync(project.Id);
        repos.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // GetSourceContextForEventAsync — no linked repo
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSourceContextForEvent_ReturnsNull_WhenNoRepoLinked()
    {
        await using var ctx = Ctx();
        var encryption = CreateEncryption();
        var service = CreateService(ctx, encryption);

        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var evt = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);

        var result = await service.GetSourceContextForEventAsync(evt.Id, "src/foo.py", 10);

        result.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // GetSourceContextForEventAsync — with linked repo + mock client
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetSourceContextForEvent_CallsClientAndReturnsContext()
    {
        await using var ctx = Ctx();
        var encryption = CreateEncryption();

        // Set up mock client
        var mockClient = Substitute.For<ISourceCodeClient>();
        mockClient.ProviderType.Returns(ProviderType.GitHub);
        var sourceLines = new List<SourceLine> { new(10, "    raise ValueError('oops')") };
        mockClient
            .GetSourceContextAsync(Arg.Any<ResolvedRepository>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<int>())
            .Returns(Task.FromResult<SourceContextLines?>(new SourceContextLines(sourceLines, "src/handler.py")));

        var service = CreateService(ctx, encryption, mockClient);

        // Create project, release with a commit SHA, and an event linked to that release
        var project = await TestHelper.CreateProjectAsync(ctx);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, "v1.0.0");

        // Set the commit SHA on the release
        release.CommitSha = "abc123";
        await ctx.SaveChangesAsync();

        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        var evt = new CapturedEvent
        {
            EventId = Guid.NewGuid().ToString("N")[..32],
            Timestamp = DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow,
            Level = Severity.Error,
            Platform = Platform.Python,
            IssueId = issue.Id,
            ProjectId = project.Id,
            ReleaseId = release.Id,
            RawCompressedJson = [],
        };
        ctx.Events.Add(evt);
        await ctx.SaveChangesAsync();

        // Link a provider + repo to the project
        var provider = await CreateProviderAsync(ctx, encryption, type: ProviderType.GitHub);
        var repo = new ProjectRepository
        {
            ProjectId = project.Id,
            ProviderId = provider.Id,
            RepositoryOwner = "my-org",
            RepositoryName = "my-service",
            DefaultBranch = "main",
        };
        ctx.ProjectRepositories.Add(repo);
        await ctx.SaveChangesAsync();

        var result = await service.GetSourceContextForEventAsync(evt.Id, "src/handler.py", 10);

        result.Should().NotBeNull();
        result!.Filename.Should().Be("src/handler.py");
        result.TargetLine.Should().Be(10);
        result.Lines.Should().HaveCount(1);
        result.Lines[0].LineNumber.Should().Be(10);
        result.Lines[0].Content.Should().Be("    raise ValueError('oops')");

        // Verify the client was called with the correct commit SHA
        await mockClient.Received(1).GetSourceContextAsync(
            Arg.Any<ResolvedRepository>(),
            "src/handler.py",
            10,
            "abc123",
            Arg.Any<int>());
    }
}
