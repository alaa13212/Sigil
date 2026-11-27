using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Sigil.Application.Interfaces;
using Sustainsys.Saml2;
using Sustainsys.Saml2.AspNetCore2;
using Sustainsys.Saml2.Configuration;
using Sustainsys.Saml2.Metadata;

namespace Sigil.Server.Auth.Saml;

internal sealed class SamlOptionsConfigurator(
    SamlConnectionRegistry registry,
    IAppConfigService configService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<SamlOptionsConfigurator> logger) : IConfigureNamedOptions<Saml2Options>
{
    public void Configure(Saml2Options options) => Configure(SamlConstants.Scheme, options);

    public void Configure(string? name, Saml2Options options)
    {
        if (name != SamlConstants.Scheme) return;

        options.SignInScheme = SamlConstants.ExternalScheme;
        options.SignOutScheme = IdentityConstants.ApplicationScheme;
        options.SPOptions.ModulePath = SamlConstants.ModulePath;
        options.SPOptions.EntityId = new EntityId(GetEntityId());
        options.Notifications.EmitSameSiteNone = _ => IsSecureOrigin();
        options.Notifications.GetPublicOrigin = _ => GetPublicOrigin();

        foreach (var existing in options.IdentityProviders.KnownIdentityProviders.ToList())
        {
            options.IdentityProviders.Remove(existing.EntityId);
        }

        foreach (var connection in registry.Current.Connections)
        {
            try
            {
                var identityProvider = new IdentityProvider(new EntityId(connection.EntityId), options.SPOptions);
                identityProvider.ReadMetadata(connection.Metadata);
                options.IdentityProviders.Add(identityProvider);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The identity provider {EntityId} could not be configured.", connection.EntityId);
            }
        }
    }

    private string GetEntityId()
    {
        var hostUrl = configService.HostUrl;
        if (string.IsNullOrWhiteSpace(hostUrl)) return SamlConstants.DefaultEntityId;

        return hostUrl.TrimEnd('/') + SamlConstants.ModulePath;
    }

    private Uri? GetPublicOrigin()
    {
        var hostUrl = configService.HostUrl;
        if (string.IsNullOrWhiteSpace(hostUrl)) return null;

        return Uri.TryCreate(hostUrl, UriKind.Absolute, out var uri) ? uri : null;
    }

    private bool IsSecureOrigin()
    {
        var origin = GetPublicOrigin();
        if (origin is not null)
            return string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        return httpContextAccessor.HttpContext?.Request.IsHttps == true;
    }
}
