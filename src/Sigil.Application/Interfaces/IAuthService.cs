using Sigil.Application.Models.Auth;

namespace Sigil.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<List<UserInfo>> GetAllUsersAsync();
    Task<InviteResult> InviteUserAsync(InviteRequest request);
    Task<AuthResult> ActivateAccountAsync(ActivateRequest request);
    Task SetUserAdminAsync(Guid userId, bool isAdmin);
}
