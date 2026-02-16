namespace Sigil.Application.Interfaces;

public interface ICacheService
{
    static abstract string CategoryName { get; }
}

public static class CacheServiceExtensions
{
    public static string Category<T>(this T cacheService) where T : ICacheService => T.CategoryName;
    public static string GetKey<T>(this T cacheService, string key) where T : ICacheService => $"{T.CategoryName}:{key}";
}