using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Extensions;

namespace Sigil.Infrastructure.Persistence;

internal class TagService(SigilDbContext dbContext, ITagCache tagCache) : ITagService
{
    public async Task<IReadOnlyCollection<TagKey>> BulkGetOrCreateTagKeysAsync(IReadOnlyCollection<string> keys)
    {
        if (keys.IsEmpty())
            return [];
        
        var (results, misses) = tagCache.TryGetMany(keys, k =>
        {
            tagCache.TryGetKey(k, out var v);
            if (v is not null) dbContext.Attach(v);
            return v;
        });

        if (misses.Count > 0)
        {
            var fromDb = await dbContext.TagKeys
                .AsTracking()
                .Include(tk => tk.Values)
                .Where(tk => misses.Contains(tk.Key))
                .ToListAsync();

            var existingKeys = fromDb.Select(tk => tk.Key).ToList();
            var newKeys = misses
                .Where(k => !existingKeys.Contains(k))
                .Select(k => new TagKey { Key = k })
                .ToList();

            if (newKeys.Any())
            {
                dbContext.TagKeys.AddRange(newKeys);
                await dbContext.SaveChangesAsync();
                fromDb.AddRange(newKeys);
            }

            foreach (TagKey tagKey in fromDb)
                tagCache.SetKey(tagKey);

            results.AddRange(fromDb);
        }

        return results;
    }

    public async Task<IReadOnlyCollection<TagValue>> BulkGetOrCreateTagsAsync(IReadOnlyCollection<KeyValuePair<string, string>> tags)
        => await BulkGetOrCreateTagsAsync([], tags);

    public async Task<IReadOnlyCollection<TagValue>> BulkGetOrCreateTagsAsync(IReadOnlyCollection<TagKey> tagKeys, IReadOnlyCollection<KeyValuePair<string, string>> tags)
    {
        if (tags.IsEmpty())
            return [];

        var (results, misses) = tagCache.TryGetMany(tags, tag =>
        {
            tagCache.TryGetValue(tag.Key, tag.Value, out var tagValue);
            if (tagValue is not null)
                dbContext.Attach(tagValue);
            return tagValue;
        });

        if (misses.Count > 0)
        {
            Dictionary<string, TagKey> allTagKeys = tagKeys.ToDictionary(k => k.Key);
            List<string> tagsWithoutKeys = misses.Select(t => t.Key).Where(k => allTagKeys.All(tk => tk.Key != k)).Distinct().ToList();
            if (tagsWithoutKeys.Any())
            {
                IReadOnlyCollection<TagKey> newKeys = await BulkGetOrCreateTagKeysAsync(tagsWithoutKeys);
                foreach (var tagKey in newKeys)
                    allTagKeys.TryAdd(tagKey.Key, tagKey);
            }

            var keys = misses.Select(t => t.Key).Distinct().ToList();
            var candidates = await dbContext.TagValues
                .AsTracking()
                .Include(tv => tv.TagKey)
                .Where(tv => keys.Contains(tv.TagKey!.Key))
                .ToListAsync();

            List<TagValue> existing = candidates
                .Where(tv => misses.Any(t => t.Key == tv.TagKey!.Key && t.Value == tv.Value))
                .ToList();

            List<TagValue> newTagValues = misses
                .Where(t => !existing.Any(tv => tv.TagKey!.Key == t.Key && tv.Value == t.Value))
                .Select(t => new TagValue { TagKey = allTagKeys[t.Key], Value = t.Value })
                .ToList();

            if (newTagValues.Any())
            {
                dbContext.TagValues.AddRange(newTagValues);
                await dbContext.SaveChangesAsync();
                existing.AddRange(newTagValues);
            }

            foreach (TagValue tagValue in existing)
                tagCache.SetValue(tagValue);

            results.AddRange(misses.Select(tag =>
                existing.First(tv => tv.TagKey!.Key == tag.Key && tv.Value == tag.Value)));
        }

        return results;
    }
}
