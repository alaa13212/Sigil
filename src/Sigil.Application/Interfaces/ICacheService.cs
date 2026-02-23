namespace Sigil.Application.Interfaces;

public interface ICacheService
{
    static abstract string CategoryName { get; }
}

public readonly record struct CacheQueryResult<TKey, TModel>(List<TModel> Hits, List<TKey> Misses);

public static class CacheServiceExtensions
{
    public static string Category<T>(this T cacheService) where T : ICacheService => T.CategoryName;
    public static string GetKey<T>(this T cacheService, string key) where T : ICacheService => $"{T.CategoryName}:{key}";

    /// <summary>
    /// Partitions <paramref name="keys"/> into cache hits and misses using the provided lookup delegate.
    /// The delegate returns the cached value, or <c>null</c> on a miss.
    /// </summary>
    public static CacheQueryResult<TKey, TModel> TryGetMany<TKey, TModel>(
        this ICacheService _,
        IEnumerable<TKey> keys,
        Func<TKey, TModel?> tryGet) where TModel : class
    {
        List<TModel> hits = [];
        List<TKey> misses = [];

        foreach (var key in keys)
        {
            var value = tryGet(key);
            if (value is not null)
                hits.Add(value);
            else
                misses.Add(key);
        }

        return new CacheQueryResult<TKey, TModel>(hits, misses);
    }
}