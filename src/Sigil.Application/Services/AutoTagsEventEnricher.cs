using Sigil.Application.Interfaces;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

internal class AutoTagsEventEnricher(IAutoTagService autoTagService) : IEventEnricher
{
    public void Enrich(ParsedEvent parsedEvent, EventParsingContext context)
    {
        autoTagService.ApplyRules(parsedEvent, context.AutoTagRules);
    }
}
