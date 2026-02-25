using Sigil.Application.Interfaces;
using Sigil.Domain;
using Sigil.Domain.Ingestion;

namespace Sigil.Infrastructure.Parsing;

internal class EventParsingContextBuilder(
    INormalizationRuleService normalizationRuleService,
    IAutoTagService autoTagService,
    IEventFilterService filterService,
    IProjectConfigService projectConfigService) : IEventParsingContextBuilder
{
    public async Task<EventParsingContext> BuildAsync(int projectId)
    {
        var normTask      = normalizationRuleService.GetRawRulesAsync(projectId);
        var autoTagTask   = autoTagService.GetRawRulesForProjectAsync(projectId);
        var filterTask    = filterService.GetRawFiltersForProjectAsync(projectId);
        var hvThreshTask  = projectConfigService.GetAsync(projectId, ProjectConfigKeys.HighVolumeThreshold);

        await Task.WhenAll(normTask, autoTagTask, filterTask, hvThreshTask);

        int highVolumeThreshold = int.TryParse(hvThreshTask.Result, out var parsed) ? parsed : 1000;

        return new EventParsingContext
        {
            ProjectId = projectId,
            NormalizationRules = normTask.Result,
            AutoTagRules = autoTagTask.Result,
            InboundFilters = filterTask.Result,
            HighVolumeThreshold = highVolumeThreshold,
        };
    }
}
