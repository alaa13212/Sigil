namespace Sigil.Application.Interfaces;

public interface IProjectConfigCache : ICacheService
{
    static string ICacheService.CategoryName => "projectconfig";

    bool TryGet(int projectId, string key, out string? value);
    void Set(int projectId, string key, string? value);
    void Invalidate(int projectId, string key);
    void InvalidateAll(int projectId);
}
