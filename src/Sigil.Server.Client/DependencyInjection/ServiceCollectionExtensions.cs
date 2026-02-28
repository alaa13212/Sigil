using Sigil.Application.Interfaces;
using Sigil.Server.Client.Services;

namespace Sigil.Server.Client.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers client infrastructure services: HTTP wrappers for all API services and stack frame cleaners.
    /// </summary>
    public static void AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, ApiAuthService>();
        services.AddScoped<ITeamService, ApiTeamService>();
        services.AddScoped<ISetupService, ApiSetupService>();
        services.AddScoped<IProjectService, ApiProjectService>();
        services.AddScoped<IRecommendationService, ApiRecommendationService>();
        services.AddScoped<IIssueService, ApiIssueService>();
        services.AddScoped<IEventService, ApiEventService>();
        services.AddScoped<IIssueActivityService, ApiIssueActivityService>();
        services.AddScoped<IDigestionMonitorService, ApiDigestionMonitorService>();
        services.AddScoped<IPasskeyService, ApiPasskeyService>();
        services.AddScoped<IEventFilterService, ApiEventFilterService>();
        services.AddScoped<IMergeSetService, ApiMergeSetService>();
        services.AddScoped<IBookmarkService, ApiBookmarkService>();
        services.AddScoped<IAlertService, ApiAlertService>();
        services.AddScoped<IAlertChannelService, ApiAlertChannelService>();
        services.AddScoped<IAutoTagService, ApiAutoTagService>();
        services.AddScoped<INormalizationRuleService, ApiNormalizationRuleService>();
        services.AddScoped<IReleaseHealthService, ApiReleaseHealthService>();
        services.AddScoped<IBadgeService, ApiBadgeService>();
        services.AddScoped<ISearchService, ApiSearchService>();
        services.AddScoped<IStackTraceFilterService, ApiStackTraceFilterService>();
        services.AddScoped<IUserPreferenceService, LocalStoragePreferenceService>();
        services.AddScoped<IAppConfigEditorService, ApiAppConfigEditorService>();
        services.AddScoped<IProjectConfigEditorService, ApiProjectConfigEditorService>();
        services.AddScoped<ISharedLinkService, ApiSharedLinkService>();
    }
}
