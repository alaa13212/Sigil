using Sigil.Application.Models.Auth;

namespace Sigil.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task LogoutAsync();
}
