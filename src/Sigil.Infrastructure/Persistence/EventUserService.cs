using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.Infrastructure.Persistence;

internal class EventUserService(SigilDbContext dbContext, IEventUserCache eventUserCache) : IEventUserService
{
    public async Task<List<EventUser>> BulkGetOrCreateEventUsersAsync(IReadOnlyCollection<ParsedEventUser> eventUsers)
    {
        // Deduplicate and merge by unique identifier (moved from old EventUserCache)
        List<ParsedEventUser> validUsers = eventUsers
            .Where(u => u.UniqueIdentifier != null)
            .GroupBy(u => u.UniqueIdentifier)
            .Select(group => group.Aggregate(ParsedEventUser.Merge))
            .ToList();

        var (results, misses) = eventUserCache.TryGetMany(validUsers, u =>
        {
            eventUserCache.TryGet(u.UniqueIdentifier!, out EventUser? cached);
            return cached;
        });

        if (misses.Count > 0)
        {
            List<string> allKeys = misses.Select(u => u.UniqueIdentifier!).ToList();

            List<EventUser> fromDb = await dbContext.EventUsers
                .Where(u => allKeys.Contains(u.UniqueIdentifier))
                .ToListAsync();

            List<string> existingKeys = fromDb.Select(u => u.UniqueIdentifier).ToList();
            List<ParsedEventUser> newUsers = misses.Where(u => !existingKeys.Contains(u.UniqueIdentifier!)).ToList();

            if (newUsers.Any())
            {
                List<EventUser> newEntities = newUsers.Select(u => new EventUser
                {
                    UniqueIdentifier = u.UniqueIdentifier!,
                    Identifier = u.Id,
                    Username = u.Username,
                    Email = u.Email,
                    IpAddress = u.IpAddress,
                    Data = u.Data,
                }).ToList();

                dbContext.EventUsers.AddRange(newEntities);
                await dbContext.SaveChangesAsync();

                foreach (EventUser eu in newEntities)
                    dbContext.Entry(eu).State = EntityState.Detached;

                fromDb.AddRange(newEntities);
            }

            foreach (EventUser user in fromDb)
                eventUserCache.Set(user);

            results.AddRange(fromDb);
        }

        return results;
    }
}
