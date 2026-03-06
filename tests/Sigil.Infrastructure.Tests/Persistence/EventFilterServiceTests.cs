using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Filters;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class EventFilterServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime()
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        return dt;
    }

    private static IEventFilterCache StubCache()
    {
        var cache = Substitute.For<IEventFilterCache>();
        cache.TryGet(Arg.Any<int>(), out Arg.Any<List<EventFilter>?>()).Returns(false);
        return cache;
    }

    private static IRuleEngine StubRuleEngine() => Substitute.For<IRuleEngine>();

    [Fact]
    public async Task CreateFilter_PersistsAndReturnsResponse()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = new EventFilterService(ctx, StubCache(), StubDateTime(), StubRuleEngine());

        var result = await service.CreateFilterAsync(project.Id, new CreateFilterRequest(
            "message", FilterOperator.Contains, "error"));

        result.Id.Should().BeGreaterThan(0);
        result.ProjectId.Should().Be(project.Id);
        result.Field.Should().Be("message");
        result.Action.Should().Be(FilterAction.Reject);
    }

    [Fact]
    public async Task GetFilters_ReturnsFiltersOrderedByPriority()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = new EventFilterService(ctx, StubCache(), StubDateTime(), StubRuleEngine());

        await service.CreateFilterAsync(project.Id, new("f1", FilterOperator.Equals, "v1", Priority: 20));
        await service.CreateFilterAsync(project.Id, new("f2", FilterOperator.Equals, "v2", Priority: 5));

        var filters = await service.GetFiltersAsync(project.Id);

        filters.Should().HaveCount(2);
        filters[0].Priority.Should().BeLessThanOrEqualTo(filters[1].Priority);
    }

    [Fact]
    public async Task UpdateFilter_ModifiesExisting()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = new EventFilterService(ctx, StubCache(), StubDateTime(), StubRuleEngine());
        var created = await service.CreateFilterAsync(project.Id, new("message", FilterOperator.Contains, "old"));

        var updated = await service.UpdateFilterAsync(created.Id, new UpdateFilterRequest(
            "environment", FilterOperator.Equals, "staging", FilterAction.Reject, false, 10, "Updated"));

        updated.Should().NotBeNull();
        updated.Field.Should().Be("environment");
        updated.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateFilter_NonExistent_ReturnsNull()
    {
        await using var ctx = Ctx();
        var service = new EventFilterService(ctx, StubCache(), StubDateTime(), StubRuleEngine());

        var result = await service.UpdateFilterAsync(999999, new("f", FilterOperator.Equals, "v", FilterAction.Reject, true, 0, null));

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteFilter_RemovesFromDb()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var service = new EventFilterService(ctx, StubCache(), StubDateTime(), StubRuleEngine());
        var created = await service.CreateFilterAsync(project.Id, new("message", FilterOperator.Contains, "err"));

        var deleted = await service.DeleteFilterAsync(created.Id);

        deleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteFilter_NonExistent_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var service = new EventFilterService(ctx, StubCache(), StubDateTime(), StubRuleEngine());

        (await service.DeleteFilterAsync(999999)).Should().BeFalse();
    }

    [Fact]
    public void ShouldRejectEvent_MatchingEnabledFilter_ReturnsTrue()
    {
        var ruleEngine = StubRuleEngine();
        ruleEngine.Evaluate(Arg.Any<RuleCondition>(), Arg.Any<ParsedEvent>()).Returns(true);
        var service = new EventFilterService(null!, null!, null!, ruleEngine);

        var filters = new List<EventFilter>
        {
            new() { Field = "message", Operator = FilterOperator.Contains, Value = "err", Action = FilterAction.Reject, Enabled = true },
        };
        var evt = new ParsedEvent
        {
            EventId = "1", Timestamp = DateTime.UtcNow, Platform = Platform.CSharp,
            Level = Severity.Error, RawJson = "{}", Message = "error",
        };

        service.ShouldRejectEvent(evt, filters).Should().BeTrue();
    }

    [Fact]
    public void ShouldRejectEvent_DisabledFilter_ReturnsFalse()
    {
        var ruleEngine = StubRuleEngine();
        var service = new EventFilterService(null!, null!, null!, ruleEngine);

        var filters = new List<EventFilter>
        {
            new() { Field = "message", Operator = FilterOperator.Contains, Value = "err", Action = FilterAction.Reject, Enabled = false },
        };
        var evt = new ParsedEvent
        {
            EventId = "1", Timestamp = DateTime.UtcNow, Platform = Platform.CSharp,
            Level = Severity.Error, RawJson = "{}",
        };

        service.ShouldRejectEvent(evt, filters).Should().BeFalse();
    }

    [Fact]
    public async Task CreateFilter_InvalidatesCache()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var cache = StubCache();
        var service = new EventFilterService(ctx, cache, StubDateTime(), StubRuleEngine());

        await service.CreateFilterAsync(project.Id, new("f", FilterOperator.Equals, "v"));

        cache.Received(1).Invalidate(project.Id);
    }

    [Fact]
    public async Task UpdateFilter_InvalidatesCache()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var cache = StubCache();
        var service = new EventFilterService(ctx, cache, StubDateTime(), StubRuleEngine());
        var created = await service.CreateFilterAsync(project.Id, new("message", FilterOperator.Contains, "err"));
        cache.ClearReceivedCalls();

        await service.UpdateFilterAsync(created.Id, new("environment", FilterOperator.Equals, "prod", FilterAction.Reject, true, 10, null));

        cache.Received(1).Invalidate(project.Id);
    }

    [Fact]
    public async Task DeleteFilter_InvalidatesCache()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var cache = StubCache();
        var service = new EventFilterService(ctx, cache, StubDateTime(), StubRuleEngine());
        var created = await service.CreateFilterAsync(project.Id, new("message", FilterOperator.Contains, "err"));
        cache.ClearReceivedCalls();

        await service.DeleteFilterAsync(created.Id);

        cache.Received(1).Invalidate(project.Id);
    }
}
