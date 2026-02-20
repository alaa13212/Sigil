using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

public class NormalizedMessageEnricher(IMessageNormalizer normalizer, INormalizationRuleService normalizationRuleService) : IEventEnricher
{
    public async Task Enrich(ParsedEvent parsedEvent, int projectId)
    {
        if (!parsedEvent.Message.IsNullOrEmpty())
        {
            List<TextNormalizationRule> rules = await normalizationRuleService.GetRawRulesAsync(projectId);
            parsedEvent.NormalizedMessage = normalizer.NormalizeMessage(rules, parsedEvent.Message);
        }
    }
}
