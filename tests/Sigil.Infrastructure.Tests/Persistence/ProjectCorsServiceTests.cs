using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;
using Sigil.Domain;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class ProjectCorsServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private ProjectConfigService CreateConfigService()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SigilDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        return new ProjectConfigService(services.BuildServiceProvider());
    }

    private ProjectCorsService CreateService(SigilDbContext ctx, IProjectConfigService? configService = null)
    {
        var editor = new ProjectConfigEditorService(ctx, configService ?? Substitute.For<IProjectConfigService>(), StubDateTime());
        return new ProjectCorsService(editor);
    }

    private async Task<string?> ReadStoredValueAsync(int projectId)
    {
        await using var verifyCtx = Ctx();
        var entry = await verifyCtx.ProjectConfigs
            .FirstOrDefaultAsync(c => c.ProjectId == projectId && c.Key == ProjectConfigKeys.AllowedCorsOrigins);
        return entry?.Value;
    }

    [Fact]
    public async Task SetOriginsAsync_ValidOrigins_PersistsNormalizedValue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);

        var result = await service.SetOriginsAsync(project.Id, new SetCorsOriginsRequest
        {
            Origins = ["HTTPS://Example.com", "http://localhost:3000"]
        });

        result.Should().Equal("https://example.com", "http://localhost:3000");
        (await ReadStoredValueAsync(project.Id)).Should().Be("https://example.com,http://localhost:3000");
    }

    [Fact]
    public async Task SetOriginsAsync_InvalidOrigin_ThrowsAndDoesNotPersist()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);

        var act = () => service.SetOriginsAsync(project.Id, new SetCorsOriginsRequest
        {
            Origins = ["https://good.com", "not-an-origin"]
        });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not-an-origin*");
        (await ReadStoredValueAsync(project.Id)).Should().BeNull();
    }

    [Fact]
    public async Task SetOriginsAsync_EmptyList_ClearsValue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);

        await service.SetOriginsAsync(project.Id, new SetCorsOriginsRequest { Origins = ["https://a.com"] });
        var result = await service.SetOriginsAsync(project.Id, new SetCorsOriginsRequest { Origins = [] });

        result.Should().BeEmpty();
        (await ReadStoredValueAsync(project.Id)).Should().BeNull();
    }

    [Fact]
    public async Task SetOriginsAsync_DuplicateOrigins_StoresEachOnce()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);

        var result = await service.SetOriginsAsync(project.Id, new SetCorsOriginsRequest
        {
            Origins = ["https://a.com", "https://a.com", "HTTPS://A.com"]
        });

        result.Should().Equal("https://a.com");
        (await ReadStoredValueAsync(project.Id)).Should().Be("https://a.com");
    }

    [Fact]
    public async Task SetOriginsAsync_CommaSeparatedEntry_SplitsIntoMultipleOrigins()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);

        var result = await service.SetOriginsAsync(project.Id, new SetCorsOriginsRequest
        {
            Origins = ["https://a.com,https://b.com"]
        });

        result.Should().Equal("https://a.com", "https://b.com");
    }

    [Fact]
    public async Task GetOriginsAsync_ReturnsStoredOrigins()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);

        await service.SetOriginsAsync(project.Id, new SetCorsOriginsRequest { Origins = ["https://a.com"] });

        var result = await service.GetOriginsAsync(project.Id);

        result.Should().Equal("https://a.com");
    }

    [Fact]
    public async Task GetOriginsAsync_NoConfiguredOrigins_ReturnsEmpty()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = CreateService(ctx);

        var result = await service.GetOriginsAsync(project.Id);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SetOriginsAsync_RefreshesCachedAccessor()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var configService = CreateConfigService();
        await configService.LoadAsync();
        var service = CreateService(ctx, configService);

        await service.SetOriginsAsync(project.Id, new SetCorsOriginsRequest { Origins = ["https://a.com"] });

        configService.AllowedCorsOrigins(project.Id).Should().Equal("https://a.com");
    }
}
