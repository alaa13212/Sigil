using Sigil.Domain.Entities;

namespace Sigil.Application.Interfaces;

public interface ITagService
{
    Task<IReadOnlyCollection<TagValue>> BulkGetOrCreateTagsAsync(IReadOnlyCollection<KeyValuePair<string, string>> tags);
}
