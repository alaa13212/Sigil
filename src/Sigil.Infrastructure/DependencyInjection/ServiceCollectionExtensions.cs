using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Services;
using Sigil.Domain.Entities;
using Sigil.Domain.Interfaces;
using Sigil.Infrastructure.Cache;
using Sigil.Infrastructure.Parsing;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;
using Sigil.Infrastructure.Workers;

namespace Sigil.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static void AddInfrastructure(this IServiceCollection services, IConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        
        // Database configuration
        services.AddDbContextPool<SigilDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });
        
        services.AddScoped<IDatabaseMigrator, EfDatabaseMigrator>();
        
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddScoped<IEventParser, SentryEventParser>();
        services.AddScoped<IEventParsingContextBuilder, EventParsingContextBuilder>();
        services.AddScoped<IEventRanker, EventRanker>();
        services.AddSingleton<IHashGenerator, DefaultHashGenerator>();
        services.AddScoped<ICompressionService, GzipCompressionService>();
        services.AddScoped<IDateTime, DateTimeProvider>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IReleaseService, ReleaseService>();
        services.AddScoped<IEventUserService, EventUserService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IIssueActivityService, IssueActivityService>();
        services.AddScoped<IAppConfigService, AppConfigService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<IRawEnvelopeService, RawEnvelopeService>();
        services.AddScoped<IDigestionService, DigestionService>();
        services.AddScoped<IDigestionMonitorService, DigestionMonitorService>();
        services.AddScoped<IPasskeyService, PasskeyService>();
        services.AddScoped<IEventFilterService, EventFilterService>();
        services.AddScoped<IAutoTagService, AutoTagService>();
        services.AddScoped<INormalizationRuleService, NormalizationRuleService>();
        services.AddScoped<IMergeSetService, MergeSetService>();
        services.AddScoped<IBookmarkService, BookmarkService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IReleaseHealthService, ReleaseHealthService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddAllImplementations<IProjectAnalyzer>();
        
        services.AddHttpClient<SlackAlertSender>();
        services.AddHttpClient<WebhookAlertSender>();
        services.AddScoped<IAlertSender>(UseExisting<SlackAlertSender>);
        services.AddScoped<IAlertSender>(UseExisting<WebhookAlertSender>);
        
        services.AddIdentityServices();
        services.AddPasskeyServices();
        services.AddCaches();
        services.AddWorkers(configuration);
    }

    private static void AddCaches(this IServiceCollection services)
    {
        services.AddSingleton<ICacheManager, CacheManager>();
        services.Configure<CacheManagerOptions>(options =>
        {
            options.Add<IAppConfigCache>(50, TimeSpan.FromHours(1));
            options.Add<IProjectCache>(100, TimeSpan.FromMinutes(30));
            options.Add<IReleaseCache>(5_000, TimeSpan.FromMinutes(30));
            options.Add<ITagCache>(20_000, TimeSpan.FromMinutes(30));
            options.Add<IIssueCache>(10_000, TimeSpan.FromMinutes(30));
            options.Add<IEventUserCache>(20_000, TimeSpan.FromMinutes(5));
            options.Add<IEventFilterCache>(500, TimeSpan.FromHours(1));
            options.Add<IAutoTagRuleCache>(500, TimeSpan.FromHours(1));
            options.Add<INormalizationRuleCache>(500, TimeSpan.FromHours(1));
        });

        services.AddScoped<IAppConfigCache, AppConfigCache>();
        services.AddScoped<IProjectCache, ProjectCache>();
        services.AddScoped<IReleaseCache, ReleaseCache>();
        services.AddScoped<ITagCache, TagCache>();
        services.AddScoped<IIssueCache, IssueCache>();
        services.AddScoped<IEventUserCache, EventUserCache>();
        services.AddScoped<IEventFilterCache, EventFilterCache>();
        services.AddScoped<IAutoTagRuleCache, AutoTagRuleCache>();
        services.AddScoped<INormalizationRuleCache, NormalizationRuleCache>();
    }
    
    private static void AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentity<User, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<SigilDbContext>()
            .AddDefaultTokenProviders();
    }

    private static void AddPasskeyServices(this IServiceCollection services)
    {
        services.AddSingleton<PasskeyChallengeStore>();
    }

    private static void AddWorkers(this IServiceCollection services, IConfigurationManager configuration)
    {
        services.AddHostedService<WorkersHost>();
        services.AddSingleton<IDigestionSignal, DigestionSignal>();
        services.AddWorker<IEventIngestionWorker, EventIngestionWorker>();
        
        services.AddSingleton<DigestionWorker>();
        services.AddSingleton<IWorker>(UseExisting<DigestionWorker>);
        
        services.AddSingleton<PostDigestionWorker>();
        services.AddSingleton<IWorker<PostDigestionWork>>(UseExisting<PostDigestionWorker>);
        services.AddSingleton<IWorker>(UseExisting<PostDigestionWorker>);

        services.AddSingleton<RecommendationAnalysisWorker>();
        services.AddSingleton<IWorker>(UseExisting<RecommendationAnalysisWorker>);

        services.Configure<BatchWorkersConfig>(configuration.GetSection("BatchWorkers"));
    }

    private static void AddWorker<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface, IWorker
    {
        services.AddSingleton<TInterface, TImplementation>();
        services.AddSingleton<IWorker>(sp => (IWorker) UseExisting<TInterface>(sp));
    }


    private static IServiceCollection AddAllImplementations<TService>(this IServiceCollection services, Assembly? searchAssembly = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        return services.AddAllImplementations(typeof(TService), searchAssembly, lifetime);
    }

    private static IServiceCollection AddAllImplementations(this IServiceCollection services, Type serviceType, Assembly? searchAssembly = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        if (searchAssembly == null)
            searchAssembly = typeof(AssemblyMarker).Assembly;

        IEnumerable<Type> implementations = searchAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(serviceType));

        foreach (var implementation in implementations)
        {
            services.Add(new ServiceDescriptor(serviceType, implementation, lifetime));
        }
        return services;
    }

    private static T UseExisting<T>(IServiceProvider sp) where T : notnull => sp.GetRequiredService<T>();
}