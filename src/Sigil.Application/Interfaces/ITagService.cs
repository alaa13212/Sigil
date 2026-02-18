using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface ITagService
{
    Task<IReadOnlyCollection<TagKey>> BulkGetOrCreateTagKeysAsync(IReadOnlyCollection<string> keys);
    Task<IReadOnlyCollection<TagValue>> BulkGetOrCreateTagsAsync(IReadOnlyCollection<KeyValuePair<string, string>> tags);
    Task<IReadOnlyCollection<TagValue>> BulkGetOrCreateTagsAsync(IReadOnlyCollection<TagKey> tagKeys, IReadOnlyCollection<KeyValuePair<string, string>> tags);
}
