using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/account")]
public class AccountController(IAuthService authService) : SigilController
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        if (!result.Succeeded)
            return Unauthorized(new { errors = result.Errors });

        return Ok(result.User);
    }

    [Authorize]
    [HttpPost("/logout")]
    public async Task<IActionResult> Logout()
    {
        await authService.LogoutAsync();
        return Redirect("/login");
    }

    [Authorize(Policy = SigilPermissions.CanInviteUsers)]
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await authService.GetAllUsersAsync();
        return Ok(users);
    }

    [Authorize(Policy = SigilPermissions.CanInviteUsers)]
    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteRequest request)
    {
        var result = await authService.InviteUserAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return Ok(new { result.Email, result.ActivationUri });
    }

    [Authorize(Policy = SigilPermissions.CanInviteUsers)]
    [HttpPut("users/{userId:guid}/admin")]
    public async Task<IActionResult> SetAdmin(Guid userId, [FromBody] SetAdminRequest request)
    {
        await authService.SetUserAdminAsync(userId, request.IsAdmin);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateRequest request)
    {
        var result = await authService.ActivateAccountAsync(request);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return Ok(result.User);
    }
}

public record SetAdminRequest(bool IsAdmin);
