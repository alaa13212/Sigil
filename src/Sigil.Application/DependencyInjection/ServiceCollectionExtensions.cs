using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Application.Services;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Application-layer services safe for use in Blazor WebAssembly (no server dependencies).
    /// Enrichers are excluded — they only run server-side during event digestion.
    /// </summary>
    public static void AddApplicationClient(this IServiceCollection services)
    {

    }

    public static void AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IInternalTagValueFormatter, ReleaseTagValueFormatter>();
        services.AddSingleton<ITagValueFormatter, CompositeTagValueFormatter>();

        services.AddSingleton<IMessageNormalizer, MessageNormalizer>();

        services.AddSingleton<PlatformInfoProvider>();

        services.AddSingleton<IFingerprintGenerator, DefaultFingerprintGenerator>();

        services.AddSingleton<IEventEnricher, PredefinedTagsEnricher>();
        services.AddSingleton<IEventEnricher, NormalizedMessageEnricher>();
        services.AddSingleton<IEventEnricher, RemoteIpProviderEnricher>();
        services.AddSingleton<IEventEnricher, EventUserUniqueIdentifierEnricher>();
        services.AddSingleton<IEventEnricher, StackTraceFilterEnricher>();
        services.AddSingleton<IEventEnricher, FingerprintEventEnricher>();
        services.AddSingleton<IEventEnricher, AutoTagsEventEnricher>();
        
        
        services.AddSingleton<IStackFrameCleaner, CSharpStackFrameCleaner>();
        services.AddSingleton<IStackFrameCleaner, JavaStackFrameCleaner>();
        services.AddSingleton<IStackFrameCleaner, JavaScriptStackFrameCleaner>();
        services.AddSingleton<IStackFrameCleaner, PythonStackFrameCleaner>();
        services.AddSingleton<StackFrameCleanerService>();
    }
}
