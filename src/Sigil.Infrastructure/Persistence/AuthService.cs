using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class AuthService(
    UserManager<User> userManager,
    SignInManager<User> signInManager) : IAuthService
{
    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return AuthResult.Failure("Invalid email or password.");

        if (!user.EmailConfirmed)
            return AuthResult.Failure("Account is not activated. Use the invitation link to set your password.");

        var result = await signInManager.PasswordSignInAsync(user, request.Password, false, lockoutOnFailure: false);
        if (!result.Succeeded)
            return AuthResult.Failure("Invalid email or password.");

        user.LastLogin = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        return AuthResult.Success(ToUserInfo(user, roles));
    }

    public async Task LogoutAsync()
    {
        await signInManager.SignOutAsync();
    }

    public async Task<List<UserInfo>> GetAllUsersAsync()
    {
        var users = await userManager.Users
            .OrderBy(u => u.Email)
            .ToListAsync();

        var result = new List<UserInfo>(users.Count);
        foreach (var u in users)
        {
            string? token = null;
            if (!u.EmailConfirmed)
                token = await userManager.GeneratePasswordResetTokenAsync(u);

            result.Add(new UserInfo(u.Id, u.Email!, u.DisplayName, u.CreatedAt, u.LastLogin, [], u.EmailConfirmed, token));
        }
        return result;
    }

    public async Task<InviteResult> InviteUserAsync(InviteRequest request)
    {
        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            return InviteResult.Failure(result.Errors.Select(e => e.Description));

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        return InviteResult.Success(user.Email!, token);
    }

    public async Task<AuthResult> ActivateAccountAsync(ActivateRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return AuthResult.Failure("Invalid activation link.");

        if (user.EmailConfirmed)
            return AuthResult.Failure("Account is already activated.");

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.Password);
        if (!result.Succeeded)
            return AuthResult.Failure(result.Errors.Select(e => e.Description));

        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);

        return AuthResult.Success(ToUserInfo(user, []));
    }

    private static UserInfo ToUserInfo(User user, IList<string> roles) =>
        new(user.Id, user.Email!, user.DisplayName, user.CreatedAt, user.LastLogin, roles.ToList(), user.EmailConfirmed);
}
