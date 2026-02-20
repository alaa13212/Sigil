using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class NormalizedMessageEnricher(IMessageNormalizer normalizer) : IEventEnricher
{
    public Task Enrich(ParsedEvent parsedEvent, int projectId)
    {
        if (!parsedEvent.Message.IsNullOrEmpty())
        {
            parsedEvent.NormalizedMessage = normalizer.NormalizeMessage(parsedEvent.Message);
        }
        
        return Task.CompletedTask;
    }
}