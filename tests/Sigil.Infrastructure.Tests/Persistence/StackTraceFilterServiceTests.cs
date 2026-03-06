using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Filters;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class StackTraceFilterServiceTests(TestDatabaseFixture fixture)
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

    private static IStackTraceFilterCache StubCache()
    {
        var cache = Substitute.For<IStackTraceFilterCache>();
        cache.TryGet(Arg.Any<int>(), out Arg.Any<List<StackTraceFilter>?>()).Returns(false);
        return cache;
    }

    private async Task<int> CreateTestProjectAsync()
    {
        await using var context = CreateContext();
        var project = new Project
        {
            Name = $"STFilterTest-{Guid.NewGuid():N}",
            Platform = Platform.CSharp,
            ApiKey = Guid.NewGuid().ToString("N"),
        };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    [Fact]
    public async Task CreateFilter_PersistsAndReturnsResponse()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new StackTraceFilterService(context, StubCache(), StubDateTime());

        var result = await service.CreateFilterAsync(projectId, new CreateStackTraceFilterRequest(
            "function", FilterOperator.Contains, "System.",
            Enabled: true, Priority: 0, Description: "Hide System frames"));

        result.Id.Should().BeGreaterThan(0);
        result.ProjectId.Should().Be(projectId);
        result.Field.Should().Be("function");
        result.Value.Should().Be("System.");
    }

    [Fact]
    public async Task GetFilters_ReturnsFiltersForProject()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new StackTraceFilterService(context, StubCache(), StubDateTime());

        await service.CreateFilterAsync(projectId, new("function", FilterOperator.Contains, "Internal",
            true, 10, null));
        await service.CreateFilterAsync(projectId, new("module", FilterOperator.Equals, "System.Core",
            true, 20, null));

        var filters = await service.GetFiltersAsync(projectId);

        filters.Should().HaveCount(2);
        filters.Should().AllSatisfy(f => f.ProjectId.Should().Be(projectId));
    }

    [Fact]
    public async Task UpdateFilter_ModifiesExistingFilter()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new StackTraceFilterService(context, StubCache(), StubDateTime());
        var created = await service.CreateFilterAsync(projectId, new("function", FilterOperator.Contains, "old",
            true, 0, "Original"));

        var updated = await service.UpdateFilterAsync(created.Id, new UpdateStackTraceFilterRequest(
            "module", FilterOperator.Equals, "new-value", false, 5, "Updated"));

        updated.Should().NotBeNull();
        updated.Field.Should().Be("module");
        updated.Value.Should().Be("new-value");
        updated.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateFilter_NonExistentId_ReturnsNull()
    {
        await using var context = CreateContext();
        var service = new StackTraceFilterService(context, StubCache(), StubDateTime());

        var result = await service.UpdateFilterAsync(999999, new("f", FilterOperator.Equals, "v", true, 0, null));

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteFilter_RemovesFromDatabase()
    {
        var projectId = await CreateTestProjectAsync();
        await using var context = CreateContext();
        var service = new StackTraceFilterService(context, StubCache(), StubDateTime());
        var created = await service.CreateFilterAsync(projectId, new("function", FilterOperator.Contains, "test",
            true, 0, null));

        var deleted = await service.DeleteFilterAsync(created.Id);

        deleted.Should().BeTrue();

        await using var verifyCtx = CreateContext();
        var inDb = await verifyCtx.StackTraceFilters.FindAsync(created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteFilter_NonExistentId_ReturnsFalse()
    {
        await using var context = CreateContext();
        var service = new StackTraceFilterService(context, StubCache(), StubDateTime());

        var result = await service.DeleteFilterAsync(999999);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CreateFilter_InvalidatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new StackTraceFilterService(context, cache, StubDateTime());

        await service.CreateFilterAsync(projectId, new("function", FilterOperator.Contains, "test",
            true, 0, null));

        cache.Received(1).Invalidate(projectId);
    }

    [Fact]
    public async Task UpdateFilter_InvalidatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new StackTraceFilterService(context, cache, StubDateTime());
        var created = await service.CreateFilterAsync(projectId, new("function", FilterOperator.Contains, "old",
            true, 0, null));
        cache.ClearReceivedCalls();

        await service.UpdateFilterAsync(created.Id, new("module", FilterOperator.Equals, "new", true, 5, null));

        cache.Received(1).Invalidate(projectId);
    }

    [Fact]
    public async Task DeleteFilter_InvalidatesCache()
    {
        var projectId = await CreateTestProjectAsync();
        var cache = StubCache();
        await using var context = CreateContext();
        var service = new StackTraceFilterService(context, cache, StubDateTime());
        var created = await service.CreateFilterAsync(projectId, new("function", FilterOperator.Contains, "test",
            true, 0, null));
        cache.ClearReceivedCalls();

        await service.DeleteFilterAsync(created.Id);

        cache.Received(1).Invalidate(projectId);
    }
}
