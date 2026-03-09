using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class ReleaseServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IReleaseCache MissCache()
    {
        var cache = Substitute.For<IReleaseCache>();
        cache.TryGet(Arg.Any<int>(), Arg.Any<string>(), out Arg.Any<Release?>()).Returns(false);
        return cache;
    }

    private static ReleaseService Create(SigilDbContext ctx, IReleaseCache? cache = null)
        => new(ctx, cache ?? MissCache());

    private static ParsedEvent MakeEvent(string? release = null) => new()
    {
        EventId = Guid.NewGuid().ToString("N"),
        Timestamp = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = Severity.Error,
        RawJson = "{}",
        Release = release,
    };

    [Fact]
    public async Task BulkGetOrCreate_EventsWithNullRelease_ReturnsEmpty()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx);

        var result = await service.BulkGetOrCreateReleasesAsync(project.Id,
            [MakeEvent(release: null), MakeEvent(release: null)]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BulkGetOrCreate_NewSemverRelease_ParsedAndPersisted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx);

        var result = await service.BulkGetOrCreateReleasesAsync(project.Id,
            [MakeEvent(release: "1.2.3")]);

        result.Should().HaveCount(1);
        result[0].RawName.Should().Be("1.2.3");
        result[0].SemanticVersion.Should().Be("1.2.3");
        result[0].Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task BulkGetOrCreate_SameReleaseTwice_DoesNotDuplicate()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx);

        var first = await service.BulkGetOrCreateReleasesAsync(project.Id, [MakeEvent("2.0.0")]);
        var second = await service.BulkGetOrCreateReleasesAsync(project.Id, [MakeEvent("2.0.0")]);

        first[0].Id.Should().Be(second[0].Id);

        await using var verifyCtx = Ctx();
        verifyCtx.Releases.Count(r => r.ProjectId == project.Id && r.RawName == "2.0.0").Should().Be(1);
    }

    [Fact]
    public async Task BulkGetOrCreate_PackageAndVersion_BothParsed()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx);

        var result = await service.BulkGetOrCreateReleasesAsync(project.Id,
            [MakeEvent(release: "myapp@3.1.0")]);

        result.Should().HaveCount(1);
        result[0].Package.Should().Be("myapp");
        result[0].SemanticVersion.Should().Be("3.1.0");
    }

    [Fact]
    public async Task BulkGetOrCreate_BuildNumberFormat_ParsedAndPersisted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx);

        var result = await service.BulkGetOrCreateReleasesAsync(project.Id,
            [MakeEvent(release: "build: 42")]);

        result.Should().HaveCount(1);
        result[0].Build.Should().Be(42);
    }

    [Fact]
    public async Task BulkGetOrCreate_UnrecognizedFormat_StoredAsRawOnly()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx);

        var result = await service.BulkGetOrCreateReleasesAsync(project.Id,
            [MakeEvent(release: "custom-release-tag")]);

        result.Should().HaveCount(1);
        result[0].RawName.Should().Be("custom-release-tag");
        result[0].SemanticVersion.Should().BeNull();
        result[0].Package.Should().BeNull();
        result[0].Build.Should().BeNull();
    }

    [Fact]
    public async Task BulkGetOrCreate_MultipleDistinctReleases_AllPersisted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx);
        var prefix = Guid.NewGuid().ToString("N")[..8];
        var v1 = $"{prefix}-1.0.0";
        var v2 = $"{prefix}-2.0.0";
        var v3 = $"{prefix}-3.0.0";

        var result = await service.BulkGetOrCreateReleasesAsync(project.Id,
            [MakeEvent(v1), MakeEvent(v2), MakeEvent(v3)]);

        result.Should().HaveCount(3);
        result.Select(r => r.RawName).Should().BeEquivalentTo([v1, v2, v3]);
        result.Should().AllSatisfy(r => r.Id.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task BulkGetOrCreate_ExistingReleaseInDb_ReturnedWithoutDuplication()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var existing = await TestHelper.CreateReleaseAsync(ctx, project.Id, "4.0.0");

        await using var ctx2 = Ctx();
        var service = Create(ctx2);

        var result = await service.BulkGetOrCreateReleasesAsync(project.Id,
            [MakeEvent("4.0.0")]);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(existing.Id);

        await using var verifyCtx = Ctx();
        verifyCtx.Releases.Count(r => r.ProjectId == project.Id && r.RawName == "4.0.0").Should().Be(1);
    }

    [Fact]
    public async Task BulkGetOrCreate_CacheMiss_SetsReleaseInCache()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var cache = MissCache();
        var service = Create(ctx, cache);

        await service.BulkGetOrCreateReleasesAsync(project.Id, [MakeEvent("5.0.0")]);

        cache.Received().Set(project.Id, Arg.Is<Release>(r => r.RawName == "5.0.0"));
    }

    [Fact]
    public async Task BulkGetOrCreate_CacheHit_ReturnsWithoutDbInsert()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var cachedRelease = new Release { Id = 8888, ProjectId = project.Id, RawName = "6.0.0", FirstSeenAt = DateTime.UtcNow };
        var cache = Substitute.For<IReleaseCache>();
        cache.TryGet(project.Id, "6.0.0", out Arg.Any<Release?>())
            .Returns(x => { x[2] = cachedRelease; return true; });

        await using var ctx2 = Ctx();
        var service = Create(ctx2, cache);

        var result = await service.BulkGetOrCreateReleasesAsync(project.Id, [MakeEvent("6.0.0")]);

        result.Should().HaveCount(1);
        result[0].Should().BeSameAs(cachedRelease);
        cache.DidNotReceive().Set(Arg.Any<int>(), Arg.Any<Release>());

        await using var verifyCtx = Ctx();
        verifyCtx.Releases.Count(r => r.ProjectId == project.Id && r.RawName == "6.0.0").Should().Be(0);
    }

    [Fact]
    public async Task BulkGetOrCreate_NewRelease_AutoTransitionsResolvedInFutureIssuesToResolved()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);

        var issue = new Issue
        {
            ProjectId = project.Id,
            Title = "Bug fixed in next release",
            Fingerprint = Guid.NewGuid().ToString("N")[..32],
            ExceptionType = "TestException",
            Level = Severity.Error,
            Status = IssueStatus.ResolvedInFuture,
            Priority = Priority.Medium,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            LastChangedAt = DateTime.UtcNow,
            OccurrenceCount = 1,
            ResolvedInReleaseId = null,
        };
        ctx.Issues.Add(issue);
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var service = Create(ctx2);

        await service.BulkGetOrCreateReleasesAsync(project.Id, [MakeEvent("7.0.0")]);

        await using var verifyCtx = Ctx();
        var updated = await verifyCtx.Issues.FindAsync(issue.Id);
        updated!.Status.Should().Be(IssueStatus.Resolved);
        updated.ResolvedInReleaseId.Should().BeNull();
    }

    [Fact]
    public async Task BulkGetOrCreate_DuplicateReleaseInSameBatch_OnlyOneRowCreated()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx);

        var result = await service.BulkGetOrCreateReleasesAsync(project.Id,
            [MakeEvent("8.0.0"), MakeEvent("8.0.0")]);

        // Both events share the same release
        result.Should().HaveCount(1);

        await using var verifyCtx = Ctx();
        verifyCtx.Releases.Count(r => r.ProjectId == project.Id && r.RawName == "8.0.0").Should().Be(1);
    }
}
