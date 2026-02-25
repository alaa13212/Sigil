using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class NormalizedMessageEnricher(IMessageNormalizer normalizer) : IEventEnricher
{
    public void Enrich(ParsedEvent parsedEvent, EventParsingContext context)
    {
        if (!parsedEvent.Message.IsNullOrEmpty())
            parsedEvent.NormalizedMessage = normalizer.NormalizeMessage(context.NormalizationRules, parsedEvent.Message);
    }
}
