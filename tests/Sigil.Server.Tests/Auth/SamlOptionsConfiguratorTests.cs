using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sigil.Application.Interfaces;
using Sigil.Server.Auth.Saml;
using Sustainsys.Saml2.AspNetCore2;

namespace Sigil.Server.Tests.Auth;

public class SamlOptionsConfiguratorTests
{
    private static SamlOptionsConfigurator CreateConfigurator(string? hostUrl)
    {
        var configService = Substitute.For<IAppConfigService>();
        configService.HostUrl.Returns(hostUrl);

        var registry = new SamlConnectionRegistry(
            Substitute.For<IServiceScopeFactory>(),
            new Saml2OptionsCache(),
            NullLogger<SamlConnectionRegistry>.Instance);

        return new SamlOptionsConfigurator(
            registry,
            configService,
            new HttpContextAccessor(),
            NullLogger<SamlOptionsConfigurator>.Instance);
    }

    [Fact]
    public void Configure_SetsModulePathEntityIdAndSignInScheme()
    {
        var configurator = CreateConfigurator("https://sigil.example.com");
        var options = new Saml2Options();

        configurator.Configure("Saml2", options);

        options.SPOptions.ModulePath.Should().Be("/saml/sp");
        options.SPOptions.EntityId.Id.Should().Be("https://sigil.example.com/saml/sp");
        options.SignInScheme.Should().Be("Saml2External");
    }

    [Fact]
    public void Configure_WithoutHostUrl_UsesFallbackEntityId()
    {
        var configurator = CreateConfigurator(null);
        var options = new Saml2Options();

        configurator.Configure("Saml2", options);

        options.SPOptions.EntityId.Id.Should().Be("urn:sigil:saml");
    }

    [Fact]
    public void Configure_IgnoresOtherOptionNames()
    {
        var configurator = CreateConfigurator("https://sigil.example.com");
        var options = new Saml2Options();

        configurator.Configure("SomeOtherScheme", options);

        options.SPOptions.EntityId.Should().BeNull();
        options.SPOptions.ModulePath.Should().Be("/Saml2");
    }

    [Fact]
    public void Configure_ResolvesPublicOriginFromHostUrl()
    {
        var configurator = CreateConfigurator("https://sigil.example.com");
        var options = new Saml2Options();

        configurator.Configure("Saml2", options);

        options.Notifications.GetPublicOrigin(null!).Should().Be(new Uri("https://sigil.example.com"));
    }

    [Fact]
    public void Configure_WithoutHostUrl_HasNoPublicOrigin()
    {
        var configurator = CreateConfigurator(null);
        var options = new Saml2Options();

        configurator.Configure("Saml2", options);

        options.Notifications.GetPublicOrigin(null!).Should().BeNull();
    }

    [Fact]
    public void Configure_WithSecureHostUrl_EmitsSameSiteNone()
    {
        var configurator = CreateConfigurator("https://sigil.example.com");
        var options = new Saml2Options();

        configurator.Configure("Saml2", options);

        options.Notifications.EmitSameSiteNone("Mozilla/5.0").Should().BeTrue();
    }

    [Fact]
    public void Configure_WithInsecureHostUrl_DoesNotEmitSameSiteNone()
    {
        var configurator = CreateConfigurator("http://sigil.example.com");
        var options = new Saml2Options();

        configurator.Configure("Saml2", options);

        options.Notifications.EmitSameSiteNone("Mozilla/5.0").Should().BeFalse();
    }
}
