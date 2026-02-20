using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Sigil.Application.Interfaces;
using Sigil.Domain.Extensions;

namespace Sigil.Infrastructure.Cache;

internal class CacheManager : ICacheManager
{
    private record CategoryCache(IMemoryCache Cache, SemaphoreSlim Lock);
    
    private readonly Dictionary<string, CategoryCache> _caches = new();
    private readonly CacheManagerOptions _options;
    private readonly Lock _lock = new();

    public CacheManager(IOptions<CacheManagerOptions> options)
    {
        _options = options.Value;
        InitializeCaches();
    }

    private void InitializeCaches()
    {
        foreach (var category in _options.Categories)
        {
            var opts = new MemoryCacheOptions
            {
                SizeLimit = category.Value.SizeLimit,
            };

            if (category.Value.ExpirationScanFrequency.HasValue) 
                opts.ExpirationScanFrequency = category.Value.ExpirationScanFrequency.Value;
            
            if (category.Value.CompactOnMemoryPressure) 
                opts.CompactionPercentage = 0.2;

            _caches[category.Key] = new CategoryCache(new MemoryCache(opts), new SemaphoreSlim(1, 1));
        }
    }

    private CategoryCache GetOrCreateCache(string category)
    {
        if (_caches.TryGetValue(category, out var cache))
        {
            return cache;
        }

        lock (_lock)
        {
            if (_caches.TryGetValue(category, out cache))
            {
                return cache;
            }
        }
        
        throw new ArgumentException($"Cache for category {category} not found");
    }

    public bool TryGet<T>(string category, string key, [NotNullWhen(true)] out T? value)
    {
        var cache = GetOrCreateCache(category);
        return cache.Cache.TryGetValue(key, out value);
    }

    public void Set<T>(string category, string key, T value)
    {
        var cache = GetOrCreateCache(category);
        var options = CreateEntryOptions(category);
        cache.Cache.Set(key, value, options);
    }
    
    public async Task<T> GetOrAdd<T>(string category, string key, Func<string, Task<T>> valueFactory)
    {
        return (await GetOrAddNullable(category, key, async _ => await valueFactory(key)))!;
    }
    
    public async Task<T?> GetOrAddNullable<T>(string category, string key, Func<string, Task<T?>> valueFactory)
    {
        var cache = GetOrCreateCache(category);
        if (cache.Cache.TryGetValue(key, out T? value))
        {
            return value;
        }

        using (await cache.Lock.LockAsync())
        {
            if (cache.Cache.TryGetValue(key, out value) && value != null)
            {
                return value;
            }
            
            value = await valueFactory(key);
            Set(category, key, value);
        }
        
        return value;
    }
    
    public void Invalidate<TCacheService>(string key) where TCacheService : ICacheService
    {
        if (_caches.TryGetValue(TCacheService.CategoryName, out var cache))
        {
            cache.Cache.Remove(key);
        }
    }

    public void Invalidate(string category, string key)
    {
        if (_caches.TryGetValue(category, out var cache))
        {
            cache.Cache.Remove(key);
        }
    }

    public void InvalidateCategory(string category)
    {
        if (_caches.TryGetValue(category, out var cache))
        {
            ((MemoryCache) cache.Cache).Compact(1.0);
        }
    }

    public void InvalidateAll()
    {
        foreach (var cache in _caches.Values.Select(cache => cache.Cache).OfType<MemoryCache>())
        {
            cache.Compact(1.0);
        }
    }

    private MemoryCacheEntryOptions CreateEntryOptions(string category)
    {
        if (!_options.Categories.TryGetValue(category, out var categoryOptions))
        {
            return new MemoryCacheEntryOptions();
        }

        var options = new MemoryCacheEntryOptions();

        if (categoryOptions.AbsoluteExpiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = categoryOptions.AbsoluteExpiration;
        }

        if (categoryOptions.SlidingExpiration.HasValue)
        {
            options.SlidingExpiration = categoryOptions.SlidingExpiration;
        }

        // Set size to 1 by default for SizeLimit to work
        options.Size = 1;

        return options;
    }
}
