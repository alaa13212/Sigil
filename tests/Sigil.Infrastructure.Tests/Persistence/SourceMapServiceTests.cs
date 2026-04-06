using System.Text;
using Sigil.Application.Interfaces;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class SourceMapServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static SourceMapService CreateService(SigilDbContext ctx) =>
        new(ctx, new GzipCompressionService(), StubDateTime());

    private static Stream MapStream(string content = """{"version":3,"mappings":"AAAA","sources":[],"names":[]}""") =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));

    // Use globally unique release names to avoid IX_Releases_RawName constraint
    private static string UniqueRelease() => $"v-{Guid.NewGuid():N}";

    // ── Upload ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_NewRelease_CreatesReleaseAndSourceMap()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseName = UniqueRelease();

        var result = await CreateService(ctx).UploadAsync(project.Id, releaseName, "main.js", MapStream());

        result.MinifiedFilePath.Should().Be("main.js");
        result.OriginalSize.Should().BeGreaterThan(0);

        await using var verify = Ctx();
        verify.Releases.Any(r => r.ProjectId == project.Id && r.RawName == releaseName).Should().BeTrue();
    }

    [Fact]
    public async Task UploadAsync_ExistingRelease_ReusesRelease()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseName = UniqueRelease();
        var service = CreateService(ctx);

        await service.UploadAsync(project.Id, releaseName, "a.js", MapStream());
        await service.UploadAsync(project.Id, releaseName, "b.js", MapStream());

        await using var verify = Ctx();
        verify.Releases.Count(r => r.ProjectId == project.Id && r.RawName == releaseName).Should().Be(1);
    }

    [Fact]
    public async Task UploadAsync_SamePath_UpsertOverwritesExisting()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseName = UniqueRelease();

        var first = await CreateService(ctx).UploadAsync(project.Id, releaseName, "main.js", MapStream());

        await using var ctx2 = Ctx();
        var second = await CreateService(ctx2).UploadAsync(project.Id, releaseName, "main.js", MapStream());

        first.Id.Should().Be(second.Id);

        await using var verify = Ctx();
        var releaseInDb = verify.Releases.First(r => r.RawName == releaseName);
        verify.SourceMaps.Count(s => s.ReleaseId == releaseInDb.Id && s.MinifiedFilePath == "main.js").Should().Be(1);
    }

    [Fact]
    public async Task UploadAsync_InvalidJson_Throws()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);

        var act = async () => await CreateService(ctx).UploadAsync(project.Id, UniqueRelease(), "main.js",
            new MemoryStream(Encoding.UTF8.GetBytes("not json")));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not valid JSON*");
    }

    [Fact]
    public async Task UploadAsync_MissingMappingsField_Throws()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);

        var act = async () => await CreateService(ctx).UploadAsync(project.Id, UniqueRelease(), "main.js",
            new MemoryStream(Encoding.UTF8.GetBytes("""{"version":3}""")));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*mappings*");
    }

    // ── List ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsAllForRelease()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseName = UniqueRelease();
        var service = CreateService(ctx);
        await service.UploadAsync(project.Id, releaseName, "a.js", MapStream());
        await service.UploadAsync(project.Id, releaseName, "b.js", MapStream());

        await using var ctx2 = Ctx();
        var release = ctx2.Releases.First(r => r.ProjectId == project.Id && r.RawName == releaseName);
        var list = await CreateService(ctx2).ListAsync(release.Id);

        list.Should().HaveCount(2);
        list.Select(m => m.MinifiedFilePath).Should().Contain(["a.js", "b.js"]);
    }

    [Fact]
    public async Task ListByReleaseNameAsync_ReturnsOnlyMatchingRelease()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var r1 = UniqueRelease();
        var r2 = UniqueRelease();
        var service = CreateService(ctx);
        await service.UploadAsync(project.Id, r1, "app.js", MapStream());
        await service.UploadAsync(project.Id, r2, "other.js", MapStream());

        await using var ctx2 = Ctx();
        var list = await CreateService(ctx2).ListByReleaseNameAsync(project.Id, r1);

        list.Should().HaveCount(1);
        list[0].MinifiedFilePath.Should().Be("app.js");
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingMap_ReturnsTrue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var uploaded = await CreateService(ctx).UploadAsync(project.Id, UniqueRelease(), "del.js", MapStream());

        await using var ctx2 = Ctx();
        var deleted = await CreateService(ctx2).DeleteAsync(project.Id, uploaded.Id);

        deleted.Should().BeTrue();

        await using var verify = Ctx();
        verify.SourceMaps.Any(s => s.Id == uploaded.Id).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        (await CreateService(ctx).DeleteAsync(project.Id, 999999)).Should().BeFalse();
    }

    // ── HasSourceMaps ─────────────────────────────────────────────────────────

    [Fact]
    public async Task HasSourceMapsAsync_WithMaps_ReturnsTrue()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseName = UniqueRelease();
        await CreateService(ctx).UploadAsync(project.Id, releaseName, "has.js", MapStream());

        await using var ctx2 = Ctx();
        var release = ctx2.Releases.First(r => r.ProjectId == project.Id && r.RawName == releaseName);
        (await CreateService(ctx2).HasSourceMapsAsync(release.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task HasSourceMapsAsync_NoMaps_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var release = await TestHelper.CreateReleaseAsync(ctx, project.Id, UniqueRelease());
        (await CreateService(ctx).HasSourceMapsAsync(release.Id)).Should().BeFalse();
    }
}
