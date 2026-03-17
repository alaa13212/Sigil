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
        var normRules      = await normalizationRuleEngine.GetRawRulesAsync(projectId);
        var autoTagRules   = await autoTagRuleSource.GetRawRulesForProjectAsync(projectId);
        var inboundFilters = await filterEngine.GetRawFiltersForProjectAsync(projectId);
        var stfFilters     = await stackTraceFilterSource.GetRawFiltersForProjectAsync(projectId);

        return new EventParsingContext
        {
            ProjectId = projectId,
            NormalizationRules = normRules,
            AutoTagRules = autoTagRules,
            InboundFilters = inboundFilters,
            StackTraceFilters = stfFilters,
            HighVolumeThreshold = projectConfigService.HighVolumeThreshold(projectId),
        };
    }
}
