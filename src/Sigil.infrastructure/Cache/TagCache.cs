using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.infrastructure.Persistence;

namespace Sigil.infrastructure.Cache;

internal class TagCache(ICacheManager cacheManager, ITagService tagService, SigilDbContext db) : ITagCache
{
    private string Category => this.Category();

    public async Task<IReadOnlyCollection<TagKey>> BulkGetOrCreateTagKeysAsync(IEnumerable<string> keys)
    {
        List<TagKey> results = [];
        List<string> missingKeys = [];
        foreach (string key in keys)
        {
            if(cacheManager.TryGet(Category, key, out TagKey? result))
            {
                results.Add(result);
                db.Attach(result);
            }
            else
            {
                missingKeys.Add(key);
            }
        }

        if (missingKeys.Count > 0)
        {
            IReadOnlyCollection<TagKey> newTagKeys = await tagService.BulkGetOrCreateTagKeysAsync(missingKeys);
            results.AddRange(newTagKeys);
            
            foreach (TagKey tagKey in newTagKeys) 
                cacheManager.Set(Category, tagKey.Key, tagKey);
        }

        return results;
    }

    public async Task<IReadOnlyCollection<TagValue>> BulkGetOrCreateTagsAsync(IReadOnlyCollection<KeyValuePair<string, string>> tags) 
        => await BulkGetOrCreateTagsAsync(await BulkGetOrCreateTagKeysAsync(tags.Select(pair => pair.Key).Distinct()), tags);
    
    public async Task<IReadOnlyCollection<TagValue>> BulkGetOrCreateTagsAsync(IReadOnlyCollection<TagKey> tagKeys, IReadOnlyCollection<KeyValuePair<string, string>> tags)
    {
        List<TagValue> results = [];
        List<KeyValuePair<string, string>> missingTagValues = [];
        foreach (var tag in tags)
        {
            string cacheKey = $"{tag.Key}:{tag.Value}";
            if(cacheManager.TryGet(Category, cacheKey, out TagValue? result))
            {
                results.Add(result);
            }
            else
            {
                missingTagValues.Add(tag);
            }
        }

        if (missingTagValues.Count > 0)
        {
            IReadOnlyCollection<TagValue> newTagValues = await tagService.BulkGetOrCreateTagsAsync(tagKeys, missingTagValues);
            results.AddRange(newTagValues);
            foreach (TagValue tag in newTagValues)
            {
                string cacheKey = $"{tag.TagKey.Key}:{tag.Value}";
                cacheManager.Set(Category, cacheKey, tag);
            }
        }
        
        
        return results;
    }
}