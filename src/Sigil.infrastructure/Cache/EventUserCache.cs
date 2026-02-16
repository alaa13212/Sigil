using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;

namespace Sigil.infrastructure.Cache;

internal class EventUserCache(ICacheManager cacheManager, IEventUserService eventUserService) : IEventUserCache
{
    private string Category => this.Category();
    
    public async Task<IReadOnlyCollection<EventUser>> BulkGetEventUsers(IEnumerable<ParsedEventUser> parsedEventUsers)
    {
        List<ParsedEventUser> validUsersById = parsedEventUsers
            .Where(u => u.UniqueIdentifier != null)
            .GroupBy(u => u.UniqueIdentifier)
            .Select(group => group.Aggregate(ParsedEventUser.Merge))
            .ToList();

        List<EventUser> users = [];
        List<ParsedEventUser> missingUsers = [];
        foreach (ParsedEventUser user in validUsersById)
        {
            if (cacheManager.TryGet(Category, user.UniqueIdentifier!, out EventUser? eventUser))
            {
                users.Add(eventUser);
            }
            else
            {
                missingUsers.Add(user);
            }
        }
        
        if(missingUsers.Count > 0)
        {
            List<EventUser> createdUsers = await eventUserService.BulkGetOrCreateEventUsersAsync(missingUsers);
            users.AddRange(createdUsers);
            
            foreach (EventUser user in createdUsers) 
                cacheManager.Set(Category, user.UniqueIdentifier, user);
        }


        return users;
    }
}