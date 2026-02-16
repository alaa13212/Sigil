using System.Diagnostics.CodeAnalysis;

namespace Sigil.Application.Interfaces;

public interface ICacheManager
{
    bool TryGet<T>(string category, string key, [NotNullWhen(true)] out T? value);
    void Set<T>(string category, string key, T value);
    
    Task<T> GetOrAdd<T>(string category, string key, Func<string, Task<T>> valueFactory);
    Task<T?> GetOrAddNullable<T>(string category, string key, Func<string, Task<T?>> valueFactory);
    
    void Invalidate<TCacheService>(string key) where TCacheService : ICacheService;
    void Invalidate<TCacheService>() where TCacheService : ICacheService => InvalidateCategory(TCacheService.CategoryName);
    void InvalidateCategory(string category);
    void InvalidateAll();
}