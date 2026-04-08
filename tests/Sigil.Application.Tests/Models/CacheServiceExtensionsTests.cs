using Sigil.Application.Interfaces;

namespace Sigil.Application.Tests.Models;

public class CacheServiceExtensionsTests
{
    // Minimal concrete implementation for testing the extension method
    private sealed class StubCache : ICacheService
    {
        public static string CategoryName => "stub";
    }

    private readonly StubCache _cache = new();

    [Fact]
    public void TryGetMany_AllHits_ReturnsAllInHitsAndEmptyMisses()
    {
        var result = _cache.TryGetMany([1, 2, 3], k => $"v{k}");
        result.Hits.Should().BeEquivalentTo(["v1", "v2", "v3"]);
        result.Misses.Should().BeEmpty();
    }

    [Fact]
    public void TryGetMany_AllMisses_ReturnsEmptyHitsAndAllInMisses()
    {
        var result = _cache.TryGetMany([1, 2, 3], _ => (string?)null);
        result.Hits.Should().BeEmpty();
        result.Misses.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public void TryGetMany_MixedResults_PartitionsCorrectly()
    {
        // key 2 hits, keys 1 and 3 miss
        var result = _cache.TryGetMany([1, 2, 3], k => k == 2 ? "hit" : null);
        result.Hits.Should().ContainSingle().Which.Should().Be("hit");
        result.Misses.Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void TryGetMany_EmptyInput_ReturnsEmpty()
    {
        var result = _cache.TryGetMany(Array.Empty<int>(), _ => (string?)null);
        result.Hits.Should().BeEmpty();
        result.Misses.Should().BeEmpty();
    }
}
