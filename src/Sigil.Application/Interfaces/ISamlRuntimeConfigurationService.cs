using Sigil.Application.Models.Saml;

namespace Sigil.Application.Interfaces;

public interface ISamlRuntimeConfigurationService
{
    Task<List<SamlConnectionRuntimeConfig>> GetRuntimeConfigurationsAsync();
}
