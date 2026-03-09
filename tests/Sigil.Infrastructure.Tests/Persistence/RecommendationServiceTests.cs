using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Services;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class RecommendationServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime(DateTime? dt = null)
    {
        var stub = Substitute.For<IDateTime>();
        stub.UtcNow.Returns(dt ?? new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        return stub;
    }

    private static IProjectAnalyzer MakeAnalyzer(
        string analyzerKey,
        bool shouldTrigger,
        RecommendationSeverity severity = RecommendationSeverity.Warning,
        bool repeatable = false)
    {
        var a = Substitute.For<IProjectAnalyzer>();
        a.AnalyzerId.Returns(analyzerKey);
        a.IsRepeatable.Returns(repeatable);
        if (shouldTrigger)
        {
            a.AnalyzeAsync(Arg.Any<Project>(), Arg.Any<PlatformInfo>()).Returns(callInfo =>
            {
                var project = callInfo.Arg<Project>();
                return Task.FromResult<ProjectRecommendation?>(new ProjectRecommendation
                {
                    ProjectId = project.Id,
                    AnalyzerId = analyzerKey,
                    Severity = severity,
                    Title = $"{analyzerKey} title",
                    Description = $"{analyzerKey} description",
                });
            });
        }
        else
        {
            a.AnalyzeAsync(Arg.Any<Project>(), Arg.Any<PlatformInfo>()).Returns(Task.FromResult<ProjectRecommendation?>(null));
        }
        return a;
    }

    private RecommendationService Create(SigilDbContext ctx, IEnumerable<IProjectAnalyzer> analyzers, IDateTime? dt = null)
        => new(ctx, analyzers, new PlatformInfoProvider(), dt ?? StubDateTime());

    // ── RunAnalyzersAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Run_NewRecommendation_Inserted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var analyzer = MakeAnalyzer("key1", shouldTrigger: true);
        var service = Create(ctx, [analyzer]);

        await service.RunAnalyzersAsync(project);

        await using var verify = Ctx();
        var recs = await verify.ProjectRecommendations.Where(r => r.ProjectId == project.Id).ToListAsync();
        recs.Should().HaveCount(1);
        recs[0].AnalyzerId.Should().Be("key1");
        recs[0].Dismissed.Should().BeFalse();
    }

    [Fact]
    public async Task Run_ExistingUndismissed_Updated()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var oldTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.ProjectRecommendations.Add(new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = "key2",
            Severity = RecommendationSeverity.Info,
            Title = "Old Title",
            Description = "Old Desc",
            DetectedAt = oldTime,
            Dismissed = false,
        });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var newTime = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var analyzer = MakeAnalyzer("key2", shouldTrigger: true, severity: RecommendationSeverity.Critical);
        var service = Create(ctx2, [analyzer], StubDateTime(newTime));
        await service.RunAnalyzersAsync(project);

        await using var verify = Ctx();
        var rec = await verify.ProjectRecommendations.SingleAsync(r => r.ProjectId == project.Id && r.AnalyzerId == "key2");
        rec.Severity.Should().Be(RecommendationSeverity.Critical);
        rec.Title.Should().Be("key2 title");
        rec.DetectedAt.Should().Be(newTime);
    }

    [Fact]
    public async Task Run_ExistingDismissed_NotRepeatable_Skipped()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        ctx.ProjectRecommendations.Add(new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = "key3",
            Severity = RecommendationSeverity.Info,
            Title = "T", Description = "D",
            Dismissed = true,
            DismissedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var analyzer = MakeAnalyzer("key3", shouldTrigger: true, repeatable: false);
        var service = Create(ctx2, [analyzer]);
        await service.RunAnalyzersAsync(project);

        await using var verify = Ctx();
        var rec = await verify.ProjectRecommendations.SingleAsync(r => r.ProjectId == project.Id && r.AnalyzerId == "key3");
        rec.Dismissed.Should().BeTrue("dismissed + not repeatable → should remain dismissed");
    }

    [Fact]
    public async Task Run_ExistingDismissed_Repeatable_ReInserted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        ctx.ProjectRecommendations.Add(new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = "key4",
            Severity = RecommendationSeverity.Info,
            Title = "T", Description = "D",
            Dismissed = true,
            DismissedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var analyzer = MakeAnalyzer("key4", shouldTrigger: true, repeatable: true);
        var service = Create(ctx2, [analyzer]);
        await service.RunAnalyzersAsync(project);

        await using var verify = Ctx();
        var rec = await verify.ProjectRecommendations.SingleAsync(r => r.ProjectId == project.Id && r.AnalyzerId == "key4");
        rec.Dismissed.Should().BeFalse("repeatable → should be re-enabled");
    }

    [Fact]
    public async Task Run_ConditionCleared_ExistingDeleted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        ctx.ProjectRecommendations.Add(new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = "key5",
            Severity = RecommendationSeverity.Warning,
            Title = "T", Description = "D",
            Dismissed = false,
        });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var analyzer = MakeAnalyzer("key5", shouldTrigger: false);
        var service = Create(ctx2, [analyzer]);
        await service.RunAnalyzersAsync(project);

        await using var verify = Ctx();
        verify.ProjectRecommendations.Any(r => r.ProjectId == project.Id && r.AnalyzerId == "key5").Should().BeFalse();
    }

    [Fact]
    public async Task Run_MultipleAnalyzers_AllExecuted()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var a1 = MakeAnalyzer("multi1", shouldTrigger: true);
        var a2 = MakeAnalyzer("multi2", shouldTrigger: true);
        var a3 = MakeAnalyzer("multi3", shouldTrigger: false);
        var service = Create(ctx, [a1, a2, a3]);

        await service.RunAnalyzersAsync(project);

        await using var verify = Ctx();
        var recs = await verify.ProjectRecommendations.Where(r => r.ProjectId == project.Id).ToListAsync();
        recs.Should().HaveCount(2);
        recs.Select(r => r.AnalyzerId).Should().Contain(["multi1", "multi2"]);
    }

    [Fact]
    public async Task Run_NoAnalyzers_NoInserts()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = Create(ctx, []);

        await service.RunAnalyzersAsync(project);

        await using var verify = Ctx();
        verify.ProjectRecommendations.Any(r => r.ProjectId == project.Id).Should().BeFalse();
    }

    // ── GetRecommendationsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetRecommendations_ReturnsSortedBySeverityThenDate()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var now = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.ProjectRecommendations.AddRange(
            new ProjectRecommendation { ProjectId = project.Id, AnalyzerId = "a", Severity = RecommendationSeverity.Info, Title = "T", Description = "D", DetectedAt = now },
            new ProjectRecommendation { ProjectId = project.Id, AnalyzerId = "b", Severity = RecommendationSeverity.Critical, Title = "T", Description = "D", DetectedAt = now.AddHours(-1) },
            new ProjectRecommendation { ProjectId = project.Id, AnalyzerId = "c", Severity = RecommendationSeverity.Warning, Title = "T", Description = "D", DetectedAt = now.AddHours(-2) }
        );
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2, []).GetRecommendationsAsync(project.Id);

        result.Should().HaveCount(3);
        result[0].Severity.Should().Be(RecommendationSeverity.Critical);
        result[1].Severity.Should().Be(RecommendationSeverity.Warning);
        result[2].Severity.Should().Be(RecommendationSeverity.Info);
    }

    [Fact]
    public async Task GetRecommendations_ExcludesDismissed()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        ctx.ProjectRecommendations.AddRange(
            new ProjectRecommendation { ProjectId = project.Id, AnalyzerId = "vis", Severity = RecommendationSeverity.Info, Title = "T", Description = "D", Dismissed = false },
            new ProjectRecommendation { ProjectId = project.Id, AnalyzerId = "dis", Severity = RecommendationSeverity.Info, Title = "T", Description = "D", Dismissed = true }
        );
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var result = await Create(ctx2, []).GetRecommendationsAsync(project.Id);

        result.Should().HaveCount(1);
        result[0].AnalyzerId.Should().Be("vis");
    }

    // ── GetRecommendationCountAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetCount_CountsOnlyNonDismissed()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        ctx.ProjectRecommendations.AddRange(
            new ProjectRecommendation { ProjectId = project.Id, AnalyzerId = "c1", Severity = RecommendationSeverity.Info, Title = "T", Description = "D", Dismissed = false },
            new ProjectRecommendation { ProjectId = project.Id, AnalyzerId = "c2", Severity = RecommendationSeverity.Info, Title = "T", Description = "D", Dismissed = false },
            new ProjectRecommendation { ProjectId = project.Id, AnalyzerId = "c3", Severity = RecommendationSeverity.Info, Title = "T", Description = "D", Dismissed = true }
        );
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var count = await Create(ctx2, []).GetRecommendationCountAsync(project.Id);

        count.Should().Be(2);
    }

    // ── DismissAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Dismiss_SetsDismissedFlagAndTimestamp()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var dismissTime = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        ctx.ProjectRecommendations.Add(new ProjectRecommendation
        {
            ProjectId = project.Id, AnalyzerId = "d1",
            Severity = RecommendationSeverity.Warning, Title = "T", Description = "D", Dismissed = false
        });
        await ctx.SaveChangesAsync();

        await using var ctx2 = Ctx();
        var rec = await ctx2.ProjectRecommendations.SingleAsync(r => r.AnalyzerId == "d1" && r.ProjectId == project.Id);
        var service = Create(ctx2, [], StubDateTime(dismissTime));
        await service.DismissAsync(rec.Id);

        await using var verify = Ctx();
        var updated = await verify.ProjectRecommendations.FindAsync(rec.Id);
        updated!.Dismissed.Should().BeTrue();
        updated.DismissedAt.Should().Be(dismissTime);
    }

    [Fact]
    public async Task Dismiss_NonExistentId_DoesNotThrow()
    {
        await using var ctx = Ctx();
        var service = Create(ctx, []);

        var act = () => service.DismissAsync(999999);

        await act.Should().NotThrowAsync();
    }
}
