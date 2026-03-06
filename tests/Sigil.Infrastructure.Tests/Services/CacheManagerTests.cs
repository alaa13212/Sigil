using Microsoft.Extensions.Options;
using Sigil.Application.Interfaces;
using Sigil.Infrastructure.Cache;

namespace Sigil.Infrastructure.Tests.Services;

file class TestCacheService : ICacheService
{
    public static string CategoryName => "test-category";
}

public class CacheManagerTests
{
    private static CacheManager CreateManager(Action<CacheManagerOptions>? configure = null)
    {
        var opts = new CacheManagerOptions();
        opts.Categories["test-category"] = new CacheManagerOptions.CategoryOptions
        {
            SizeLimit = 100,
            SlidingExpiration = TimeSpan.FromMinutes(5),
        };
        configure?.Invoke(opts);
        return new CacheManager(Options.Create(opts));
    }

    [Fact]
    public void Set_ThenTryGet_ReturnsValue()
    {
        var cache = CreateManager();
        cache.Set("test-category", "key1", "value1");

        cache.TryGet<string>("test-category", "key1", out var value).Should().BeTrue();
        value.Should().Be("value1");
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var cache = CreateManager();

        cache.TryGet<string>("test-category", "nonexistent", out _).Should().BeFalse();
    }

    [Fact]
    public void Invalidate_RemovesEntry()
    {
        var cache = CreateManager();
        cache.Set("test-category", "key1", "value1");

        cache.Invalidate("test-category", "key1");

        cache.TryGet<string>("test-category", "key1", out _).Should().BeFalse();
    }

    [Fact]
    public void Invalidate_Generic_RemovesByCategory()
    {
        var cache = CreateManager();
        cache.Set("test-category", "key1", "value1");

        cache.Invalidate<TestCacheService>("key1");

        cache.TryGet<string>("test-category", "key1", out _).Should().BeFalse();
    }

    [Fact]
    public void InvalidateCategory_RemovesAllInCategory()
    {
        var cache = CreateManager();
        cache.Set("test-category", "key1", "v1");
        cache.Set("test-category", "key2", "v2");

        cache.InvalidateCategory("test-category");

        cache.TryGet<string>("test-category", "key1", out _).Should().BeFalse();
        cache.TryGet<string>("test-category", "key2", out _).Should().BeFalse();
    }

    [Fact]
    public void InvalidateAll_ClearsAllCategories()
    {
        var cache = CreateManager(opts =>
        {
            opts.Categories["other"] = new CacheManagerOptions.CategoryOptions { SizeLimit = 50 };
        });
        cache.Set("test-category", "k1", "v1");
        cache.Set("other", "k2", "v2");

        cache.InvalidateAll();

        cache.TryGet<string>("test-category", "k1", out _).Should().BeFalse();
        cache.TryGet<string>("other", "k2", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetOrAdd_CacheMiss_CallsFactory()
    {
        var cache = CreateManager();
        var callCount = 0;

        var result = await cache.GetOrAdd("test-category", "key1", async _ =>
        {
            callCount++;
            return "computed";
        });

        result.Should().Be("computed");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrAdd_CacheHit_SkipsFactory()
    {
        var cache = CreateManager();
        cache.Set("test-category", "key1", "cached");
        var callCount = 0;

        var result = await cache.GetOrAdd("test-category", "key1", async _ =>
        {
            callCount++;
            return "new";
        });

        result.Should().Be("cached");
        callCount.Should().Be(0);
    }

    [Fact]
    public async Task GetOrAddNullable_NullValue_CachesAndReturns()
    {
        var cache = CreateManager();
        var callCount = 0;

        var result1 = await cache.GetOrAddNullable<string>("test-category", "key1", async _ =>
        {
            callCount++;
            return null;
        });

        result1.Should().BeNull();
        callCount.Should().Be(1);
    }

    [Fact]
    public void GetOrCreateCache_UnknownCategory_Throws()
    {
        var cache = CreateManager();

        var act = () => cache.Set("unknown-category", "key", "value");

        act.Should().Throw<ArgumentException>().WithMessage("*unknown-category*");
    }
}
