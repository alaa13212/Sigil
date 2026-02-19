using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/passkey")]
public class PasskeyController(IPasskeyService passkeyService) : SigilController
{
    [Authorize]
    [HttpPost("register/options")]
    public async Task<IActionResult> RegisterOptions()
    {
        var userId = GetUserId();
        var options = await passkeyService.GetRegistrationOptionsAsync(userId);
        return Ok(options);
    }

    [Authorize]
    [HttpPost("register/complete")]
    public async Task<IActionResult> RegisterComplete([FromBody] PasskeyRegistrationResponse request)
    {
        var userId = GetUserId();
        var result = await passkeyService.CompleteRegistrationAsync(userId, request);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });
        return Ok(result.User);
    }

    [AllowAnonymous]
    [HttpPost("login/options")]
    public async Task<IActionResult> LoginOptions()
    {
        var options = await passkeyService.GetAssertionOptionsAsync();
        return Ok(options);
    }

    [AllowAnonymous]
    [HttpPost("login/complete")]
    public async Task<IActionResult> LoginComplete([FromBody] PasskeyAssertionResponse request)
    {
        var result = await passkeyService.CompleteAssertionAsync(request);
        if (!result.Succeeded)
            return Unauthorized(new { errors = result.Errors });
        return Ok(result.User);
    }

    [Authorize]
    [HttpGet("list")]
    public async Task<IActionResult> List()
    {
        var userId = GetUserId();
        var passkeys = await passkeyService.GetPasskeysAsync(userId);
        return Ok(passkeys);
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();
        var deleted = await passkeyService.DeletePasskeyAsync(userId, id);
        if (!deleted)
            return NotFound();
        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? throw new InvalidOperationException("User ID claim not found.");
        return Guid.Parse(claim);
    }
}
