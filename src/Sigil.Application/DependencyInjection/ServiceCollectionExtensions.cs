using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Application.Services;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IFingerprintGenerator, DefaultFingerprintGenerator>();
        services.AddSingleton<IMessageNormalizer, MessageNormalizer>();
        
        services.AddSingleton<IEventEnricher, PredefinedTagsEnricher>();
        services.AddSingleton<IEventEnricher, NormalizedMessageEnricher>();
        services.AddSingleton<IEventEnricher, RemoteIpProviderEnricher>();
        services.AddSingleton<IEventEnricher, EventUserUniqueIdentifierEnricher>();
        services.AddSingleton<IEventEnricher, StackTraceFilterEnricher>();
        services.AddSingleton<IEventEnricher, FingerprintEventEnricher>();
        services.AddSingleton<IEventEnricher, AutoTagsEventEnricher>();
        
        services.AddSingleton<IInternalTagValueFormatter, ReleaseTagValueFormatter>();
        services.AddSingleton<ITagValueFormatter, CompositeTagValueFormatter>();
    }
}