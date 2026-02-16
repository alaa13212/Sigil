using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface ITagCache : ICacheService
{
    static string ICacheService.CategoryName => "tags";
    
    Task<IReadOnlyCollection<TagKey>> BulkGetOrCreateTagKeysAsync(IEnumerable<string> keys);
    Task<IReadOnlyCollection<TagValue>> BulkGetOrCreateTagsAsync(IReadOnlyCollection<KeyValuePair<string, string>> tags);
    Task<IReadOnlyCollection<TagValue>> BulkGetOrCreateTagsAsync(IReadOnlyCollection<TagKey> tagKeys, IReadOnlyCollection<KeyValuePair<string, string>> tags);
}