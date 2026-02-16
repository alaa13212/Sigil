using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Application.Services;
using Sigil.Domain.Interfaces;
using Sigil.infrastructure.Cache;
using Sigil.infrastructure.Parsing;
using Sigil.infrastructure.Persistence;
using Sigil.infrastructure.Services;
using Sigil.infrastructure.Workers;

namespace Sigil.infrastructure.DependencyInjection;

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
        
        services.AddScoped<IEventParser, SentryEventParser>();
        services.AddScoped<IEventRanker, EventRanker>();
        services.AddScoped<IHashGenerator, DefaultHashGenerator>();
        services.AddScoped<ICompressionService, GzipCompressionService>();
        services.AddScoped<IDateTime, DateTimeProvider>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IReleaseService, ReleaseService>();
        services.AddScoped<IEventUserService, EventUserService>();
        services.AddScoped<ITagService, TagService>();

        services.AddCaches();
        services.AddWorkers(configuration);
    }

    private static void AddCaches(this IServiceCollection services)
    {
        services.AddSingleton<ICacheManager, CacheManager>();
        services.Configure<CacheManagerOptions>(options =>
        {
            options.Add<IProjectCache>(100, TimeSpan.FromMinutes(30));
            options.Add<IReleaseCache>(5_000, TimeSpan.FromMinutes(5));
            options.Add<ITagCache>(20_000, TimeSpan.FromMinutes(3));
            options.Add<IIssueCache>(10_000, TimeSpan.FromMinutes(2));
            options.Add<IEventUserCache>(20_000, TimeSpan.FromMinutes(5));
        });

        services.AddScoped<IProjectCache, ProjectCache>();
        services.AddScoped<IReleaseCache, ReleaseCache>();
        services.AddScoped<ITagCache, TagCache>();
        services.AddScoped<IIssueCache, IssueCache>();
        services.AddScoped<IEventUserCache, EventUserCache>();
    }
    
    private static void AddWorkers(this IServiceCollection services, IConfigurationManager configuration)
    {
        services.AddHostedService<WorkersHost>();
        services.AddWorker<IEventIngestionWorker, EventIngestionWorker>();

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