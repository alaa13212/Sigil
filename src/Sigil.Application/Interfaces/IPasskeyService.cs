using Sigil.Application.Models.Auth;

namespace Sigil.Application.Interfaces;

public interface IPasskeyService
{
    Task<PasskeyRegistrationOptions> GetRegistrationOptionsAsync(Guid userId);
    Task<AuthResult> CompleteRegistrationAsync(Guid userId, PasskeyRegistrationResponse response);

    Task<PasskeyAssertionOptions> GetAssertionOptionsAsync();
    Task<AuthResult> CompleteAssertionAsync(PasskeyAssertionResponse response);

    Task<List<PasskeyInfo>> GetPasskeysAsync(Guid userId);
    Task<bool> DeletePasskeyAsync(Guid userId, int passkeyId);
}
