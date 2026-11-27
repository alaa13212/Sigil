using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;

namespace Sigil.Infrastructure.Persistence;

internal class ProjectLimitsService(IProjectConfigService projectConfig, IAppConfigService appConfig) : IProjectLimitsService
{
    public Task<ProjectLimitsResponse> GetAsync(int projectId)
    {
        int defaultMaxAgeDays = appConfig.RetentionDefaultMaxAgeDays;
        int defaultMaxEvents = appConfig.RetentionDefaultMaxEvents;

        return Task.FromResult(new ProjectLimitsResponse(
            projectConfig.RetentionMaxAgeDays(projectId),
            projectConfig.RetentionMaxAgeDays(projectId) ?? defaultMaxAgeDays,
            projectConfig.RetentionMaxEventCount(projectId),
            projectConfig.RetentionMaxEventCount(projectId) ?? defaultMaxEvents,
            defaultMaxAgeDays,
            defaultMaxEvents,
            appConfig.RetentionCheckIntervalMinutes,
            appConfig.RetentionFailedEnvelopeMaxAgeDays,
            projectConfig.RateLimitMaxEventsPerWindow(projectId),
            appConfig.RateLimitDefaultProjectLimit));
    }
}
