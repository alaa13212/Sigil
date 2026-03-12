using Microsoft.AspNetCore.DataProtection;

namespace Sigil.Infrastructure.Services;

internal class TokenEncryptionService(IDataProtectionProvider protectionProvider)
{
    private readonly IDataProtector _protector =
        protectionProvider.CreateProtector("Sigil.SourceCode.AccessToken");

    public string Encrypt(string token) => _protector.Protect(token);
    public string Decrypt(string encrypted) => _protector.Unprotect(encrypted);
}
