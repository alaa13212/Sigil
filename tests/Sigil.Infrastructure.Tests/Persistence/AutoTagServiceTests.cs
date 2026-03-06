using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.AutoTags;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class AutoTagServiceTests(TestDatabaseFixture fixture)
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

    private static IAutoTagRuleCache StubCache()
    {
        var cache = Substitute.For<IAutoTagRuleCache>();
        cache.TryGet(Arg.Any<int>(), out Arg.Any<List<AutoTagRule>?>()).Returns(false);
        return cache;
    }

    private async Task<int> CreateTestProjectAsync()
    {
        await using var context = CreateContext();
        var project = new Project
        {
            Name = $"AutoTagTest-{Guid.NewGuid():N}",
            Platform = Platform.CSharp,
            ApiKey = Guid.NewGuid().ToString("N"),
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    [Fact]
    public async Task CreateRule_PersistsAndReturnsResponse()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new AutoTagService(context, StubCache(), StubDateTime());

        var result = await service.CreateRuleAsync(projectId, new CreateAutoTagRuleRequest(
            "environment", FilterOperator.Equals, "production",
            "env-type", "prod", Enabled: true, Priority: 0, Description: "Tag prod"));

        result.Id.Should().BeGreaterThan(0);
        result.ProjectId.Should().Be(projectId);
        result.TagKey.Should().Be("env-type");
        result.TagValue.Should().Be("prod");
    }

    [Fact]
    public async Task CreateRule_SystemTagKey_ThrowsInvalidOperationException()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new AutoTagService(context, StubCache(), StubDateTime());

        var act = () => service.CreateRuleAsync(projectId, new CreateAutoTagRuleRequest(
            "environment", FilterOperator.Equals, "production",
            "sigil.reserved", "value", Enabled: true, Priority: 0, Description: null));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetRulesForProject_ReturnsOnlyProjectRules()
    {
        var projectId1 = await CreateTestProjectAsync();
        var projectId2 = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new AutoTagService(context, StubCache(), StubDateTime());

        await service.CreateRuleAsync(projectId1, new("message", FilterOperator.Contains, "err",
            "tag1", "val1", true, 0, null));
        await service.CreateRuleAsync(projectId2, new("message", FilterOperator.Contains, "warn",
            "tag2", "val2", true, 0, null));

        var rules = await service.GetRulesForProjectAsync(projectId1);

        rules.Should().AllSatisfy(r => r.ProjectId.Should().Be(projectId1));
    }

    [Fact]
    public async Task UpdateRule_ModifiesFields()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new AutoTagService(context, StubCache(), StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new("message", FilterOperator.Contains, "err",
            "tag", "val", true, 0, "Original"));

        var updated = await service.UpdateRuleAsync(created.Id, new UpdateAutoTagRuleRequest(
            "environment", FilterOperator.Equals, "staging",
            "new-tag", "new-val", false, 10, "Updated"));

        updated.Should().NotBeNull();
        updated.Field.Should().Be("environment");
        updated.TagKey.Should().Be("new-tag");
        updated.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRule_SystemTagKey_ThrowsInvalidOperationException()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new AutoTagService(context, StubCache(), StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new("message", FilterOperator.Contains, "err",
            "tag", "val", true, 0, null));

        var act = () => service.UpdateRuleAsync(created.Id, new UpdateAutoTagRuleRequest(
            "message", FilterOperator.Contains, "err",
            "sigil.reserved", "val", true, 0, null));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteRule_RemovesFromDatabase()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new AutoTagService(context, StubCache(), StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new("message", FilterOperator.Contains, "err",
            "tag", "val", true, 0, null));

        var deleted = await service.DeleteRuleAsync(created.Id);

        deleted.Should().BeTrue();

        await using var verifyCtx = CreateContext();
        var inDb = await verifyCtx.AutoTagRules.FindAsync(created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteRule_NonExistentId_ReturnsFalse()
    {
        await using var context = CreateContext();
        var service = new AutoTagService(context, StubCache(), StubDateTime());

        var result = await service.DeleteRuleAsync(999999);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRule_InvalidatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new AutoTagService(context, cache, StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new("message", FilterOperator.Contains, "err",
            "tag", "val", true, 0, null));
        cache.ClearReceivedCalls();

        await service.UpdateRuleAsync(created.Id, new("environment", FilterOperator.Equals, "prod",
            "new-tag", "new-val", true, 10, null));

        cache.Received(1).Invalidate(projectId);
    }

    [Fact]
    public async Task DeleteRule_InvalidatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new AutoTagService(context, cache, StubDateTime());
        var created = await service.CreateRuleAsync(projectId, new("message", FilterOperator.Contains, "err",
            "tag", "val", true, 0, null));
        cache.ClearReceivedCalls();

        await service.DeleteRuleAsync(created.Id);

        cache.Received(1).Invalidate(projectId);
    }
}
