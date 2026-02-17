using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;
using Sigil.Domain.Entities;

namespace Sigil.infrastructure.Persistence;

internal class AuthService(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    IHttpContextAccessor httpContextAccessor) : IAuthService
{
    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return AuthResult.Failure("Invalid email or password.");

        var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
            return AuthResult.Failure("Invalid email or password.");

        user.LastLogin = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        return AuthResult.Success(ToUserInfo(user, roles));
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return AuthResult.Failure(result.Errors.Select(e => e.Description));

        await signInManager.SignInAsync(user, isPersistent: false);
        return AuthResult.Success(ToUserInfo(user, []));
    }

    public async Task LogoutAsync()
    {
        await signInManager.SignOutAsync();
    }

    private static UserInfo ToUserInfo(User user, IList<string> roles) =>
        new(user.Id, user.Email!, user.DisplayName, user.CreatedAt, user.LastLogin, roles.ToList());
}
