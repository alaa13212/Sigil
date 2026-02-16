using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Extensions;

namespace Sigil.infrastructure.Persistence;

internal class TagService(SigilDbContext dbContext) : ITagService
{
    public async Task<IReadOnlyCollection<TagKey>> BulkGetOrCreateTagKeysAsync(IReadOnlyCollection<string> keys)
    {
        if (keys.IsEmpty())
            return [];
        
        var results = new List<TagKey>(keys.Count);
        results.AddRange(
            await dbContext.TagKeys
                .AsTracking()
                .Include(tk => tk.Values)
                .Where(tk => keys.Contains(tk.Key))
                .ToListAsync()
        );
        
        var existingKeys = results.Select(tk => tk.Key).ToList();
        var newKeys = keys
            .Where(k => !existingKeys.Contains(k))
            .Select(k => new TagKey { Key = k })
            .ToList();
        
        if (newKeys.Any())
        {
            dbContext.TagKeys.AddRange(newKeys);
            await dbContext.SaveChangesAsync();
            results.AddRange(newKeys);
        }
        
        return results;
    }

    
    public async Task<IReadOnlyCollection<TagValue>> BulkGetOrCreateTagsAsync(IReadOnlyCollection<TagKey> tagKeys, IReadOnlyCollection<KeyValuePair<string, string>> tags)
    {
        if (tags.IsEmpty())
            return [];

        List<TagValue> results = [];
        Dictionary<string, TagKey> allTagKeys = tagKeys.ToDictionary(key => key.Key);
        List<string> tagsWithoutKeys = tags.Select(pair => pair.Key).Where(key => tagKeys.All(tk => tk.Key != key)).Distinct().ToList();
        if (tagsWithoutKeys.Any())
        {
            IReadOnlyCollection<TagKey> newKeys = await BulkGetOrCreateTagKeysAsync(tagsWithoutKeys);
            foreach (var tagKey in newKeys) 
                allTagKeys.Add(tagKey.Key, tagKey);
        }

        // Get all existing tag values in one query
        // Fetch all tag values with matching keys, then filter for exact key-value pairs
        var keys = tags.Select(t => t.Key).Distinct().ToList();

        var candidateTagValues = await dbContext.TagValues
            .AsTracking()
            .Include(tv => tv.TagKey)
            .Where(tv => keys.Contains(tv.TagKey.Key))
            .ToListAsync();

        // Filter to only the exact key-value pairs we need (in memory)
        List<TagValue> existingTagValues = candidateTagValues
            .Where(tv => tags.Any(t => t.Key == tv.TagKey.Key && t.Value == tv.Value))
            .ToList();
        
        List<TagValue> newTagValues = tags
            .Where(t => !existingTagValues.Any(tv => tv.TagKey.Key == t.Key && tv.Value == t.Value))
            .Select(t => new TagValue { TagKey = allTagKeys[t.Key], Value = t.Value})
            .ToList();
        
        if (newTagValues.Any())
        {
            dbContext.TagValues.AddRange(newTagValues);
            await dbContext.SaveChangesAsync();
            existingTagValues.AddRange(newTagValues);
        }

        // Return all tag values in the order they were requested
        results.AddRange(tags.Select(tag =>
            existingTagValues.First(tv =>
                tv.TagKey.Key == tag.Key && tv.Value == tag.Value)));

        return results;
    }
}
