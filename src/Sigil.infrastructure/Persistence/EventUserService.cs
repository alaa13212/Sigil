using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.infrastructure.Persistence;

internal class EventUserService(SigilDbContext dbContext) : IEventUserService
{
    public async Task<List<EventUser>> BulkGetOrCreateEventUsersAsync(IReadOnlyCollection<ParsedEventUser> eventUsers)
    {
        List<string> allKeys = eventUsers.Select(u => u.UniqueIdentifier).Where(k => k != null).ToList()!;
        
        List<EventUser> results = [];
        results.AddRange(
            await dbContext.EventUsers
                .AsTracking()
                .Where(u => allKeys.Contains(u.UniqueIdentifier))
                .ToListAsync());
        
        List<string> existingUserKeys = results.Select(u => u.UniqueIdentifier).ToList();
        List<ParsedEventUser> newEventUsers = eventUsers.Where(u => !existingUserKeys.Contains(u.UniqueIdentifier!)).ToList();


        if (newEventUsers.Any())
        {
            List<EventUser> newUsers = newEventUsers.Select(u => new EventUser
            {
                UniqueIdentifier = u.UniqueIdentifier!,
                Identifier = u.Id,
                Username = u.Username,
                Email = u.Email,
                IpAddress = u.IpAddress,
                Data = u.Data,
            }).ToList();
            
            results.AddRange(newUsers);
            dbContext.EventUsers.AddRange(newUsers);
            await dbContext.SaveChangesAsync();

            foreach (EventUser newUser in newUsers)
                dbContext.Entry(newUser).State = EntityState.Detached;
        }
        
        return results;
    }
}