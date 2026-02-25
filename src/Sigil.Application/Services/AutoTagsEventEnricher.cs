using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

internal class AutoTagsEventEnricher(IRuleEngine ruleEngine) : IEventEnricher
{
    public void Enrich(ParsedEvent parsedEvent, EventParsingContext context)
    {
        foreach (var rule in context.AutoTagRules.Where(r => r.Enabled))
        {
            if (ruleEngine.Evaluate(new RuleCondition(rule.Field, rule.Operator, rule.Value), parsedEvent))
            {
                parsedEvent.Tags ??= new Dictionary<string, string>();
                parsedEvent.Tags[rule.TagKey] = rule.TagValue;
            }
        }
    }
}
