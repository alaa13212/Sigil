using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;
using Sigil.Infrastructure.Tests.Fixtures;
using Sigil.Infrastructure.Tests.Persistence;

namespace Sigil.Infrastructure.Tests.Services;

[Collection(DbCollection)]
public class SourceMapResolverTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static string UniqueRelease() => $"v-{Guid.NewGuid():N}";

    private static Stream MapStream(string filename = "original.ts") =>
        new MemoryStream(Encoding.UTF8.GetBytes(
            $$"""{"version":3,"sources":["{{filename}}"],"names":[],"mappings":"AAAA"}"""));

    // "AAAA" = one segment: genCol=0, srcIdx=0, srcLine=0, srcCol=0
    // So (line=1, col=0) resolves to sources[0] at line 1, col 0

    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Ctx());
        services.AddSingleton<ICompressionService, GzipCompressionService>();
        return services.BuildServiceProvider();
    }

    private SourceMapResolver CreateResolver(IMemoryCache? cache = null)
    {
        cache ??= new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 });
        return new SourceMapResolver(BuildServiceProvider(), cache);
    }

    private async Task<(int releaseId, int projectId)> UploadMapAsync(string filename, Stream? stream = null)
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var releaseName = UniqueRelease();
        var svc = new SourceMapService(ctx, new GzipCompressionService(), StubDateTime());
        await svc.UploadAsync(project.Id, releaseName, filename, stream ?? MapStream());

        await using var verify = Ctx();
        var release = verify.Releases.First(r => r.ProjectId == project.Id && r.RawName == releaseName);
        return (release.Id, project.Id);
    }

    // ── Returns null when no map matches ─────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_NoMapForFile_ReturnsNull()
    {
        var (releaseId, _) = await UploadMapAsync("other.js");
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(releaseId, "main.js", 1, 0);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_NoMapForRelease_ReturnsNull()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(99999, "main.js", 1, 0);

        result.Should().BeNull();
    }

    // ── Correct resolution ────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_ValidMap_ReturnsOriginalPosition()
    {
        var (releaseId, _) = await UploadMapAsync("main.js");
        var resolver = CreateResolver();

        // Line 1 (1-based) col 0 → AAAA maps to sources[0]="original.ts" line 1, col 0
        var result = await resolver.ResolveAsync(releaseId, "main.js", 1, 0);

        result.Should().NotBeNull();
        result!.OriginalFilename.Should().Be("original.ts");
        result.OriginalLine.Should().Be(1); // 0-based srcLine=0 + 1 → 1
        result.OriginalColumn.Should().Be(0);
    }

    // ── NormalizePath: full URL stripped to path ──────────────────────────────

    [Fact]
    public async Task ResolveAsync_FullUrlFilename_MatchesByPath()
    {
        var (releaseId, _) = await UploadMapAsync("static/js/main.js");
        var resolver = CreateResolver();

        // Stored as "static/js/main.js", queried as full URL
        var result = await resolver.ResolveAsync(
            releaseId, "https://example.com/static/js/main.js", 1, 0);

        result.Should().NotBeNull();
        result!.OriginalFilename.Should().Be("original.ts");
    }

    // ── NormalizePath: ~/ prefix removed ─────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_TildePrefixedPath_Matches()
    {
        var (releaseId, _) = await UploadMapAsync("dist/app.js");
        var resolver = CreateResolver();

        // ~/dist/app.js → /dist/app.js → dist/app.js
        var result = await resolver.ResolveAsync(releaseId, "~/dist/app.js", 1, 0);

        result.Should().NotBeNull();
        result!.OriginalFilename.Should().Be("original.ts");
    }

    // ── NormalizePath: case-insensitive ───────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_DifferentCase_Matches()
    {
        var (releaseId, _) = await UploadMapAsync("Bundle.js");
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(releaseId, "BUNDLE.JS", 1, 0);

        result.Should().NotBeNull();
        result!.OriginalFilename.Should().Be("original.ts");
    }

    // ── Cache: second call does not reload from DB ────────────────────────────

    [Fact]
    public async Task ResolveAsync_CalledTwice_ReturnsSameResult()
    {
        var (releaseId, _) = await UploadMapAsync("cached.js");
        var resolver = CreateResolver();

        var first = await resolver.ResolveAsync(releaseId, "cached.js", 1, 0);
        var second = await resolver.ResolveAsync(releaseId, "cached.js", 1, 0);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        second!.OriginalFilename.Should().Be(first!.OriginalFilename);
        second.OriginalLine.Should().Be(first.OriginalLine);
    }

    // ── 1-based to 0-based conversion ────────────────────────────────────────

    [Fact]
    public async Task ResolveAsync_Line1Col0_Converts1BasedInput()
    {
        // "AACA" = [0, 0, 1, 0]: genCol=0, srcIdx=0, srcLine=1, srcCol=0 (second source line)
        var twoLineMap = new MemoryStream(Encoding.UTF8.GetBytes(
            """{"version":3,"sources":["src.ts"],"names":[],"mappings":"AAAA;AACA"}"""));
        var (releaseId, _) = await UploadMapAsync("app.js", twoLineMap);
        var resolver = CreateResolver();

        // Query line=2 (1-based) → 0-based line=1 → AACA segment at srcLine=1 → return line 2 (1-based)
        var result = await resolver.ResolveAsync(releaseId, "app.js", 2, 0);

        result.Should().NotBeNull();
        result!.OriginalFilename.Should().Be("src.ts");
        result.OriginalLine.Should().Be(2); // srcLine=1 → +1 = 2
    }
}
