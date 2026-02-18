using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface IReleaseCache : ICacheService
{
    static string ICacheService.CategoryName => "releases";

    bool TryGet(int projectId, string rawName, out Release? release);
    void Set(int projectId, Release release);
}
