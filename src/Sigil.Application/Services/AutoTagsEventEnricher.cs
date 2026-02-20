using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.Services;

internal class AutoTagsEventEnricher(IAutoTagService autoTagService) : IEventEnricher
{
    public async Task Enrich(ParsedEvent parsedEvent, int projectId)
    {
        List<AutoTagRule> autoTagRules = await autoTagService.GetRawRulesForProjectAsync(projectId);
        autoTagService.ApplyRules(parsedEvent, autoTagRules);
    }
}