using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.ApiTokens;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

/// <summary>Manages the API tokens owned by the signed-in user.</summary>
[ApiController]
[Route("api/api-tokens")]
[Authorize]
public class ApiTokensController(IApiTokenService tokenService) : SigilController
{
    /// <summary>Creates a token and returns its plaintext secret once.</summary>
    [HttpPost]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Create([FromBody] CreateApiTokenRequest request)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });

        var created = await tokenService.CreateAsync(userId.Value, request.Name.Trim(), request.ExpiresAt);
        return Ok(created);
    }

    /// <summary>Lists the tokens owned by the signed-in user.</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        return Ok(await tokenService.ListAsync(userId.Value));
    }

    /// <summary>Revokes a token owned by the signed-in user.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        bool revoked = await tokenService.RevokeAsync(userId.Value, id);
        return revoked ? NoContent() : NotFound();
    }
}
