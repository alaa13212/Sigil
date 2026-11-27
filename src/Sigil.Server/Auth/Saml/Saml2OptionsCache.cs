using Microsoft.Extensions.Options;
using Sustainsys.Saml2.AspNetCore2;

namespace Sigil.Server.Auth.Saml;

public sealed class Saml2OptionsCache : OptionsCache<Saml2Options>
{
    public void Invalidate() => TryRemove(SamlConstants.Scheme);
}
