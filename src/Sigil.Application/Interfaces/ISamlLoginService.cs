using Sigil.Application.Models.Saml;

namespace Sigil.Application.Interfaces;

public interface ISamlLoginService
{
    Task<SamlLoginResult> CompleteLoginAsync(Guid connectionId, SamlAssertion assertion);
}
