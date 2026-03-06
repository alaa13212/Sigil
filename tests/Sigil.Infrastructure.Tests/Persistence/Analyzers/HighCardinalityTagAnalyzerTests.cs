using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Persistence.Analyzers;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

[Collection(DbCollection)]
public class HighCardinalityTagAnalyzerTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    [Fact]
    public async Task Analyze_NoHighCardinalityTags_ReturnsNull()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var evt = await TestHelper.CreateEventAsync(ctx, project.Id, issue.Id);
        var tagKey = await TestHelper.CreateTagKeyAsync(ctx, $"env-{Guid.NewGuid():N}");
        var tagValue = new TagValue { TagKeyId = tagKey.Id, Value = "prod" };
        ctx.TagValues.Add(tagValue);
        await ctx.SaveChangesAsync();
        ctx.EventTags.Add(new EventTag { EventId = evt.Id, TagValueId = tagValue.Id });
        await ctx.SaveChangesAsync();
        var analyzer = new HighCardinalityTagAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Analyze_HighCardinalityTag_ReturnsRecommendation()
    {
        await using var ctx = Ctx();
        var project = await TestHelper.CreateProjectAsync(ctx);
        var issue = await TestHelper.CreateIssueAsync(ctx, project.Id);
        var tagKey = await TestHelper.CreateTagKeyAsync(ctx, $"request_id-{Guid.NewGuid():N}");

        // Create 201 unique tag values (above 200 threshold) each linked to an event
        // Batch inserts for performance
        var events = new List<CapturedEvent>();
        for (int i = 0; i < 201; i++)
        {
            var evt = new CapturedEvent
            {
                EventId = Guid.NewGuid().ToString("N")[..32],
                Timestamp = DateTime.UtcNow,
                ReceivedAt = DateTime.UtcNow,
                Level = Severity.Error,
                Platform = Platform.CSharp,
                IssueId = issue.Id,
                ProjectId = project.Id,
                RawCompressedJson = [],
            };
            events.Add(evt);
        }
        ctx.Events.AddRange(events);
        await ctx.SaveChangesAsync();

        var tagValues = new List<TagValue>();
        for (int i = 0; i < 201; i++)
        {
            tagValues.Add(new TagValue { TagKeyId = tagKey.Id, Value = $"val-{i}" });
        }
        ctx.TagValues.AddRange(tagValues);
        await ctx.SaveChangesAsync();

        var eventTags = new List<EventTag>();
        for (int i = 0; i < 201; i++)
        {
            eventTags.Add(new EventTag { EventId = events[i].Id, TagValueId = tagValues[i].Id });
        }
        ctx.EventTags.AddRange(eventTags);
        await ctx.SaveChangesAsync();

        var analyzer = new HighCardinalityTagAnalyzer(ctx);

        var result = await analyzer.AnalyzeAsync(project);

        result.Should().NotBeNull();
        result.AnalyzerId.Should().Be("high-cardinality-tags");
    }

    [Fact]
    public void Properties_AreCorrect()
    {
        using var ctx = Ctx();
        var analyzer = new HighCardinalityTagAnalyzer(ctx);

        analyzer.AnalyzerId.Should().Be("high-cardinality-tags");
        analyzer.IsRepeatable.Should().BeTrue();
    }
}
