using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface ITagCache : ICacheService
{
    // Stryker disable once String : cache category key constant; exact value is arbitrary, collision is an integration-level concern
    static string ICacheService.CategoryName => "tags";
    
    bool TryGetKey(string key, out TagKey? tagKey);
    void SetKey(TagKey tagKey);

    bool TryGetValue(string key, string value, out TagValue? tagValue);
    void SetValue(TagValue tagValue);
}
