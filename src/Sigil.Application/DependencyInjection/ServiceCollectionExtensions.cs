using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Services;
using Sigil.Domain.Interfaces;

namespace Sigil.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IFingerprintGenerator, DefaultFingerprintGenerator>();
        services.AddScoped<IMessageNormalizer, DefaultMessageNormalizer>();
        
        services.AddScoped<IEventEnricher, PredefinedTagsEnricher>();
        services.AddScoped<IEventEnricher, NormalizedMessageEnricher>();
        services.AddScoped<IEventEnricher, RemoteIpProviderEnricher>();
        services.AddScoped<IEventEnricher, EventUserUniqueIdentifierEnricher>();
    }
}