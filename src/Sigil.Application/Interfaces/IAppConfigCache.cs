namespace Sigil.Application.Interfaces;

public interface IAppConfigCache : ICacheService
{
    static string ICacheService.CategoryName => "appconfig";

    bool TryGet(string key, out string? value);
    void Set(string key, string? value);
    void Invalidate(string key);
}
