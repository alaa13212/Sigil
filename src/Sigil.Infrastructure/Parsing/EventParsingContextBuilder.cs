using Sigil.Application.Interfaces;
using Sigil.Domain.Ingestion;

namespace Sigil.Infrastructure.Parsing;

internal class EventParsingContextBuilder(
    INormalizationRuleService normalizationRuleService,
    IAutoTagService autoTagService,
    IEventFilterService filterService) : IEventParsingContextBuilder
{
    public async Task<EventParsingContext> BuildAsync(int projectId)
    {
        var normTask = normalizationRuleService.GetRawRulesAsync(projectId);
        var autoTagTask = autoTagService.GetRawRulesForProjectAsync(projectId);
        var filterTask = filterService.GetRawFiltersForProjectAsync(projectId);

        await Task.WhenAll(normTask, autoTagTask, filterTask);

        return new EventParsingContext
        {
            ProjectId = projectId,
            NormalizationRules = normTask.Result,
            AutoTagRules = autoTagTask.Result,
            InboundFilters = filterTask.Result,
        };
    }
}
