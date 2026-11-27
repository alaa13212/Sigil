using Sigil.Application.Models.Saml;

namespace Sigil.Server.Auth.Saml;

public static class SamlConstants
{
    public const string Scheme = "Saml2";
    public const string ExternalScheme = "Saml2External";
    public const string ModulePath = SamlDefaults.ModulePath;
    public const string LoginPath = SamlDefaults.LoginPath;
    public const string CompletePath = SamlDefaults.CompletePath;
    public const string DefaultEntityId = SamlDefaults.DefaultEntityId;
}
