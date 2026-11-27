using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class CurrentUserService(
    UserManager<User> userManager,
    IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        ClaimsPrincipal? principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return null;

        User? user = await userManager.GetUserAsync(principal);
        if (user is null)
            return null;

        var roles = await userManager.GetRolesAsync(user);
        return new UserInfo(user.Id, user.Email!, user.DisplayName, user.CreatedAt, user.LastLogin, roles.ToList(), user.EmailConfirmed);
    }
}
