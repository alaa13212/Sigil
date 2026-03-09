using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class TagServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static ITagCache MissCache()
    {
        var cache = Substitute.For<ITagCache>();
        cache.TryGetKey(Arg.Any<string>(), out Arg.Any<TagKey?>()).Returns(false);
        cache.TryGetValue(Arg.Any<string>(), Arg.Any<string>(), out Arg.Any<TagValue?>()).Returns(false);
        return cache;
    }

    private static TagService Create(SigilDbContext ctx, ITagCache? cache = null)
        => new(ctx, cache ?? MissCache());

    [Fact]
    public async Task BulkGetOrCreate_EmptyInput_ReturnsEmpty()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var result = await service.BulkGetOrCreateTagsAsync([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task BulkGetOrCreate_NewTag_PersistsTagKeyAndValue()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        var result = await service.BulkGetOrCreateTagsAsync(
            [new KeyValuePair<string, string>("env", "production")]);

        result.Should().HaveCount(1);
        result.Single().Value.Should().Be("production");
        result.Single().Id.Should().BeGreaterThan(0);
        result.Single().TagKey!.Key.Should().Be("env");
    }

    [Fact]
    public async Task BulkGetOrCreate_SameTagCalledTwice_DoesNotDuplicate()
    {
        await using var ctx = Ctx();
        var uniqueKey = $"dedup-{Guid.NewGuid():N}";
        var service = Create(ctx);
        var tag = new KeyValuePair<string, string>(uniqueKey, "staging");

        var first = await service.BulkGetOrCreateTagsAsync([tag]);
        var second = await service.BulkGetOrCreateTagsAsync([tag]);

        first.Single().Id.Should().Be(second.Single().Id);

        await using var verifyCtx = Ctx();
        verifyCtx.TagKeys.Count(k => k.Key == uniqueKey).Should().Be(1);
    }

    [Fact]
    public async Task BulkGetOrCreate_ExistingKeyNewValue_CreatesValueWithExistingKey()
    {
        await using var ctx = Ctx();
        var tagKey = await TestHelper.CreateTagKeyAsync(ctx, $"platform-{Guid.NewGuid():N}");

        await using var ctx2 = Ctx();
        var service = Create(ctx2);
        var result = await service.BulkGetOrCreateTagsAsync(
            [new KeyValuePair<string, string>(tagKey.Key, "newvalue")]);

        result.Should().HaveCount(1);
        result.Single().TagKeyId.Should().Be(tagKey.Id);
        result.Single().Value.Should().Be("newvalue");
    }

    [Fact]
    public async Task BulkGetOrCreate_MultipleTags_AllPersisted()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);
        var prefix = Guid.NewGuid().ToString("N")[..8];
        var tags = new[]
        {
            new KeyValuePair<string, string>($"{prefix}-browser", "chrome"),
            new KeyValuePair<string, string>($"{prefix}-os", "linux"),
            new KeyValuePair<string, string>($"{prefix}-sdk", "dotnet"),
        };

        var result = await service.BulkGetOrCreateTagsAsync(tags);

        result.Should().HaveCount(3);
        result.Select(tv => tv.Value).Should().BeEquivalentTo(["chrome", "linux", "dotnet"]);
        result.Should().AllSatisfy(tv => tv.Id.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task BulkGetOrCreate_SameKeyDifferentValues_CreatesSeparateValues()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);
        var key = $"level-{Guid.NewGuid():N}";
        var tags = new[]
        {
            new KeyValuePair<string, string>(key, "info"),
            new KeyValuePair<string, string>(key, "error"),
        };

        var result = await service.BulkGetOrCreateTagsAsync(tags);

        result.Should().HaveCount(2);
        result.Select(tv => tv.Value).Should().BeEquivalentTo(["info", "error"]);
        result.Select(tv => tv.TagKeyId).Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task BulkGetOrCreate_CacheMiss_SetsKeyAndValueInCache()
    {
        await using var ctx = Ctx();
        var cache = MissCache();
        var service = Create(ctx, cache);
        var key = $"fw-{Guid.NewGuid():N}";

        await service.BulkGetOrCreateTagsAsync(
            [new KeyValuePair<string, string>(key, "dotnet")]);

        cache.Received().SetKey(Arg.Is<TagKey>(k => k.Key == key));
        cache.Received().SetValue(Arg.Is<TagValue>(v => v.Value == "dotnet"));
    }

    [Fact]
    public async Task BulkGetOrCreate_CacheHitForValue_ReturnesCachedValueAndSkipsDbInsert()
    {
        await using var ctx = Ctx();
        var tagKey = await TestHelper.CreateTagKeyAsync(ctx, $"cached-{Guid.NewGuid():N}");

        var cachedValue = new TagValue { Id = 9999, TagKeyId = tagKey.Id, Value = "hit", TagKey = tagKey };
        var cache = Substitute.For<ITagCache>();
        cache.TryGetKey(Arg.Any<string>(), out Arg.Any<TagKey?>()).Returns(false);
        cache.TryGetValue(tagKey.Key, "hit", out Arg.Any<TagValue?>())
            .Returns(x => { x[2] = cachedValue; return true; });

        await using var ctx2 = Ctx();
        var service = Create(ctx2, cache);

        var result = await service.BulkGetOrCreateTagsAsync(
            [new KeyValuePair<string, string>(tagKey.Key, "hit")]);

        result.Should().HaveCount(1);
        result.Single().Should().BeSameAs(cachedValue);
        cache.DidNotReceive().SetValue(Arg.Any<TagValue>());
    }
}
