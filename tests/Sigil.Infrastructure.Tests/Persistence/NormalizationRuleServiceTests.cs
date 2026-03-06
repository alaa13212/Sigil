using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.NormalizationRules;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class NormalizationRuleServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SigilDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;
        return new SigilDbContext(options);
    }

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static INormalizationRuleCache StubCache()
    {
        var cache = Substitute.For<INormalizationRuleCache>();
        cache.TryGet(Arg.Any<int>(), out Arg.Any<List<TextNormalizationRule>?>()).Returns(false);
        return cache;
    }

    private async Task<int> CreateTestProjectAsync()
    {
        await using var context = CreateContext();
        var project = new Project
        {
            Name = $"NormRuleTest-{Guid.NewGuid():N}",
            Platform = Platform.CSharp,
            ApiKey = Guid.NewGuid().ToString("N"),
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    [Fact]
    public async Task CreateRule_PersistsAndReturnsEntity()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());

        var rule = await service.CreateRuleAsync(projectId, new CreateNormalizationRuleRequest(
            @"\d+", "<NUM>", Priority: 100, Enabled: true, Description: "Numbers"));

        rule.Id.Should().BeGreaterThan(0);
        rule.Pattern.Should().Be(@"\d+");
        rule.Replacement.Should().Be("<NUM>");
        rule.ProjectId.Should().Be(projectId);
    }

    [Fact]
    public async Task GetRules_ReturnsOrderedByPriorityDescending()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());

        await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: "Low"));
        await service.CreateRuleAsync(projectId, new(@"\w+", "<WORD>", Priority: 100, Enabled: true, Description: "High"));

        var rules = await service.GetRulesAsync(projectId);

        rules.Should().HaveCountGreaterOrEqualTo(2);
        rules.First().Priority.Should().BeGreaterThanOrEqualTo(rules.Last().Priority);
    }

    [Fact]
    public async Task UpdateRule_ModifiesExistingRule()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: "Original"));

        var updated = await service.UpdateRuleAsync(created.Id,
            new UpdateNormalizationRuleRequest(@"\d{3,}", "<BIGNUM>", Priority: 20, Enabled: false, Description: "Updated"));

        updated.Should().NotBeNull();
        updated.Pattern.Should().Be(@"\d{3,}");
        updated.Replacement.Should().Be("<BIGNUM>");
        updated.Priority.Should().Be(20);
        updated.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRule_NonExistentId_ReturnsNull()
    {
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());

        var result = await service.UpdateRuleAsync(999999,
            new UpdateNormalizationRuleRequest("x", "y", 0, true, null));

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRule_RemovesFromDatabase()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: null));

        var deleted = await service.DeleteRuleAsync(created.Id);

        deleted.Should().BeTrue();

        await using var verifyCtx = CreateContext();
        var inDb = await verifyCtx.TextNormalizationRules.FindAsync(created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRule_NonExistentId_ReturnsFalse()
    {
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, StubCache(), StubDateTime());

        var deleted = await service.DeleteRuleAsync(999999);

        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateRule_InvalidatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, cache, StubDateTime());

        await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: null));

        cache.Received(1).Invalidate(projectId);
    }

    [Fact]
    public async Task UpdateRule_InvalidatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, cache, StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: null));
        cache.ClearReceivedCalls();

        await service.UpdateRuleAsync(created.Id, new(@"\w+", "<WORD>", Priority: 20, Enabled: false, Description: null));

        cache.Received(1).Invalidate(projectId);
    }

    [Fact]
    public async Task DeleteRule_InvalidatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new NormalizationRuleService(context, cache, StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new(@"\d+", "<NUM>", Priority: 10, Enabled: true, Description: null));
        cache.ClearReceivedCalls();

        await service.DeleteRuleAsync(created.Id);

        cache.Received(1).Invalidate(projectId);
    }
}
