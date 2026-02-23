using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Cache;

internal class TagCache(ICacheManager cacheManager) : ITagCache
{
    private string Category => this.Category();

    public bool TryGetKey(string key, out TagKey? tagKey) =>
        cacheManager.TryGet(Category, key, out tagKey);

    public void SetKey(TagKey tagKey) =>
        cacheManager.Set(Category, tagKey.Key, tagKey);

    public bool TryGetValue(string key, string value, out TagValue? tagValue) =>
        cacheManager.TryGet(Category, $"{key}:{value}", out tagValue);

    public void SetValue(TagValue tagValue) =>
        cacheManager.Set(Category,
            tagValue.TagKey is not null
                ? $"{tagValue.TagKey.Key}:{tagValue.Value}"
                : throw new ArgumentException("TagValue.TagKey must not be null", nameof(tagValue)),
            tagValue);
}