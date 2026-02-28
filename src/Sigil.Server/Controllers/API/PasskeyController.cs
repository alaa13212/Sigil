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
        if(userId is null)
            return Unauthorized();
        
        var options = await passkeyService.GetRegistrationOptionsAsync(userId.Value);
        return Ok(options);
    }

    [Authorize]
    [HttpPost("register/complete")]
    public async Task<IActionResult> RegisterComplete([FromBody] PasskeyRegistrationResponse request)
    {
        var userId = GetUserId();
        if(userId is null)
            return Unauthorized();

        var result = await passkeyService.CompleteRegistrationAsync(userId.Value, request);
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
        if(userId is null)
            return Unauthorized();

        var passkeys = await passkeyService.GetPasskeysAsync(userId.Value);
        return Ok(passkeys);
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();
        if(userId is null)
            return Unauthorized();

        var deleted = await passkeyService.DeletePasskeyAsync(userId.Value, id);
        if (!deleted)
            return NotFound();
        return NoContent();
    }

}
