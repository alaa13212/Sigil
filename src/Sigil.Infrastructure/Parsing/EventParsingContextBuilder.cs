using Sigil.Application.Interfaces;
using Sigil.Domain.Ingestion;

namespace Sigil.Infrastructure.Parsing;

internal class EventParsingContextBuilder(
    INormalizationRuleService normalizationRuleService,
    IAutoTagService autoTagService,
    IEventFilterService filterService,
    IStackTraceFilterService stackTraceFilterService,
    IProjectConfigService projectConfigService) : IEventParsingContextBuilder
{
    public async Task<EventParsingContext> BuildAsync(int projectId)
    {
        var normTask    = normalizationRuleService.GetRawRulesAsync(projectId);
        var autoTagTask = autoTagService.GetRawRulesForProjectAsync(projectId);
        var filterTask  = filterService.GetRawFiltersForProjectAsync(projectId);
        var stfTask     = stackTraceFilterService.GetRawFiltersForProjectAsync(projectId);

        await Task.WhenAll(normTask, autoTagTask, filterTask, stfTask);

        return new EventParsingContext
        {
            ProjectId = projectId,
            NormalizationRules = normTask.Result,
            AutoTagRules = autoTagTask.Result,
            InboundFilters = filterTask.Result,
            StackTraceFilters = stfTask.Result,
            HighVolumeThreshold = projectConfigService.HighVolumeThreshold(projectId),
        };
    }
}
