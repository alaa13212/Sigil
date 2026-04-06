using Microsoft.AspNetCore.DataProtection;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.IssueTrackers;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class IssueTrackerServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    // Share one provider per test-class instance so all encrypt/decrypt calls use the same key
    private readonly EphemeralDataProtectionProvider _dpProvider = new();

    private TokenEncryptionService CreateEncryption() => new(_dpProvider);

    private static IAppConfigService StubAppConfig(string hostUrl = "https://sigil.test")
    {
        var svc = Substitute.For<IAppConfigService>();
        svc.HostUrl.Returns(hostUrl);
        return svc;
    }

    private IssueTrackerService CreateService(
        SigilDbContext ctx,
        IIssueTrackerClient? mockClient = null)
    {
        var encryption = CreateEncryption();
        var clients = mockClient != null
            ? (IEnumerable<IIssueTrackerClient>)[mockClient]
            : [];
        return new IssueTrackerService(ctx, encryption, clients, StubDateTime(), StubAppConfig());
    }

    private static string SampleConfig(TrackerType type) => type switch
    {
        TrackerType.GitHub => """{"owner":"acme","repo":"app","pat":"ghp_test"}""",
        TrackerType.Jira => """{"baseUrl":"https://acme.atlassian.net","email":"a@b.com","apiToken":"tok","projectKey":"APP"}""",
        _ => """{"apiKey":"key","teamId":"team"}"""
    };

    // ── Config CRUD ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AddConfigAsync_EncryptsAndPersists()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);

        var result = await service.AddConfigAsync(project.Id,
            new CreateTrackerConfigRequest(TrackerType.GitHub, SampleConfig(TrackerType.GitHub)));

        result.ProjectId.Should().Be(project.Id);
        result.TrackerType.Should().Be(TrackerType.GitHub);

        // Config is stored encrypted — raw DB value should not be plain JSON
        await using var verify = Ctx();
        var stored = verify.IssueTrackerConfigs.First(c => c.Id == result.Id);
        stored.EncryptedConfig.Should().NotBe(SampleConfig(TrackerType.GitHub));
    }

    [Fact]
    public async Task GetConfigsAsync_ReturnsOnlyProjectConfigs()
    {
        await using var ctx = Ctx();
        var p1 = await TestHelper.CreateProjectAsync(ctx);
        var p2 = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);
        await service.AddConfigAsync(p1.Id, new CreateTrackerConfigRequest(TrackerType.GitHub, "{}"));
        await service.AddConfigAsync(p2.Id, new CreateTrackerConfigRequest(TrackerType.Jira, "{}"));

        await using var ctx2 = Ctx();
        var configs = await CreateService(ctx2).GetConfigsAsync(p1.Id);

        configs.Should().HaveCount(1);
        configs[0].TrackerType.Should().Be(TrackerType.GitHub);
    }

    [Fact]
    public async Task UpdateConfigAsync_UpdatesEncryptedConfig()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);
        var added = await service.AddConfigAsync(project.Id,
            new CreateTrackerConfigRequest(TrackerType.GitHub, """{"old":"config"}"""));

        await using var ctx2 = Ctx();
        var updated = await CreateService(ctx2).UpdateConfigAsync(project.Id, added.Id,
            new UpdateTrackerConfigRequest("""{"new":"config"}""", true, false));

        updated.Should().BeTrue();

        await using var verify = Ctx();
        var encryption = CreateEncryption();
        var stored = verify.IssueTrackerConfigs.First(c => c.Id == added.Id);
        var decrypted = encryption.Decrypt(stored.EncryptedConfig);
        decrypted.Should().Be("""{"new":"config"}""");
    }

    [Fact]
    public async Task UpdateConfigAsync_UpdatesEnabledAndTwoWaySync()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);
        var added = await service.AddConfigAsync(project.Id,
            new CreateTrackerConfigRequest(TrackerType.Linear, "{}"));

        await using var ctx2 = Ctx();
        await CreateService(ctx2).UpdateConfigAsync(project.Id, added.Id,
            new UpdateTrackerConfigRequest(null, false, true));

        await using var verify = Ctx();
        var stored = verify.IssueTrackerConfigs.First(c => c.Id == added.Id);
        stored.Enabled.Should().BeFalse();
        stored.TwoWaySync.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteConfigAsync_RemovesFromDb()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);
        var added = await service.AddConfigAsync(project.Id,
            new CreateTrackerConfigRequest(TrackerType.GitHub, "{}"));

        await using var ctx2 = Ctx();
        var deleted = await CreateService(ctx2).DeleteConfigAsync(project.Id, added.Id);

        deleted.Should().BeTrue();

        await using var verify = Ctx();
        verify.IssueTrackerConfigs.Any(c => c.Id == added.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteConfigAsync_NonExistentId_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var result = await CreateService(ctx).DeleteConfigAsync(project.Id, 999999);
        result.Should().BeFalse();
    }

    // ── External links ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateExternalIssueAsync_CallsClientAndPersistsLink()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        var mockClient = Substitute.For<IIssueTrackerClient>();
        mockClient.TrackerType.Returns(TrackerType.GitHub);
        mockClient.CreateIssueAsync(Arg.Any<TrackerConfig>(), Arg.Any<CreateExternalIssueRequest>())
            .Returns(new ExternalIssueResult("42", "https://github.com/acme/app/issues/42", "open"));

        var service = CreateService(ctx, mockClient);
        var config = await service.AddConfigAsync(project.Id,
            new CreateTrackerConfigRequest(TrackerType.GitHub, SampleConfig(TrackerType.GitHub)));

        await using var ctx2 = Ctx();
        var svc2 = CreateService(ctx2, mockClient);
        var link = await svc2.CreateExternalIssueAsync(issue.Id, config.Id);

        link.ExternalId.Should().Be("42");
        link.ExternalUrl.Should().Be("https://github.com/acme/app/issues/42");
        link.ExternalStatus.Should().Be("open");
    }

    [Fact]
    public async Task GetLinksAsync_ReturnsLinksForIssue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        var mockClient = Substitute.For<IIssueTrackerClient>();
        mockClient.TrackerType.Returns(TrackerType.GitHub);
        mockClient.CreateIssueAsync(Arg.Any<TrackerConfig>(), Arg.Any<CreateExternalIssueRequest>())
            .Returns(new ExternalIssueResult("1", "https://github.com/acme/app/issues/1", "open"));

        var service = CreateService(ctx, mockClient);
        var config = await service.AddConfigAsync(project.Id,
            new CreateTrackerConfigRequest(TrackerType.GitHub, SampleConfig(TrackerType.GitHub)));
        await service.CreateExternalIssueAsync(issue.Id, config.Id);

        await using var ctx2 = Ctx();
        var links = await CreateService(ctx2).GetLinksAsync(issue.Id);

        links.Should().HaveCount(1);
        links[0].IssueId.Should().Be(issue.Id);
    }

    [Fact]
    public async Task UnlinkAsync_RemovesLink()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);

        var mockClient = Substitute.For<IIssueTrackerClient>();
        mockClient.TrackerType.Returns(TrackerType.GitHub);
        mockClient.CreateIssueAsync(Arg.Any<TrackerConfig>(), Arg.Any<CreateExternalIssueRequest>())
            .Returns(new ExternalIssueResult("99", "https://github.com/u/r/issues/99", "open"));

        var service = CreateService(ctx, mockClient);
        var config = await service.AddConfigAsync(project.Id,
            new CreateTrackerConfigRequest(TrackerType.GitHub, SampleConfig(TrackerType.GitHub)));
        var link = await service.CreateExternalIssueAsync(issue.Id, config.Id);

        await using var ctx2 = Ctx();
        var unlinked = await CreateService(ctx2).UnlinkAsync(project.Id, link.Id);

        unlinked.Should().BeTrue();
        (await CreateService(ctx2).GetLinksAsync(issue.Id)).Should().BeEmpty();
    }
}
