using Sigil.Application.Interfaces;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class EventUserUniqueIdentifierEnricher(IHashGenerator hashGenerator) : IEventEnricher
{
    public void Enrich(ParsedEvent parsedEvent, EventParsingContext context)
    {
        if (parsedEvent.User != null)
        {
            if (parsedEvent.User.Id.IsNullOrEmpty() && parsedEvent.User.Username.IsNullOrEmpty() && parsedEvent.User.Email.IsNullOrEmpty() && parsedEvent.User.IpAddress.IsNullOrEmpty())
            {
                parsedEvent.User = null;
                return;
            }
            
            string? uniqueIdentifier = null;
            if(!parsedEvent.User.Id.IsNullOrEmpty())
                uniqueIdentifier = parsedEvent.User.Id;
            else if(!parsedEvent.User.Email.IsNullOrEmpty())
                uniqueIdentifier = parsedEvent.User.Email;
            else if(!parsedEvent.User.Username.IsNullOrEmpty())
                uniqueIdentifier = parsedEvent.User.Username;
            else if (!parsedEvent.User.IpAddress.IsNullOrEmpty())
                uniqueIdentifier = parsedEvent.User.IpAddress;

            parsedEvent.User.UniqueIdentifier = hashGenerator.ComputeHash(uniqueIdentifier!);
        }
        
    }
}