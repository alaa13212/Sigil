using Sigil.Application.Interfaces;
using Sigil.Domain.Ingestion;

namespace Sigil.Infrastructure.Parsing;

internal class EventParsingContextBuilder(
    INormalizationRuleEngine normalizationRuleEngine,
    IAutoTagRuleSource autoTagRuleSource,
    IEventFilterEngine filterEngine,
    IStackTraceFilterSource stackTraceFilterSource,
    IProjectConfigService projectConfigService) : IEventParsingContextBuilder
{
    public async Task<EventParsingContext> BuildAsync(int projectId)
    {
        var normTask    = normalizationRuleEngine.GetRawRulesAsync(projectId);
        var autoTagTask = autoTagRuleSource.GetRawRulesForProjectAsync(projectId);
        var filterTask  = filterEngine.GetRawFiltersForProjectAsync(projectId);
        var stfTask     = stackTraceFilterSource.GetRawFiltersForProjectAsync(projectId);

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
