using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.SourceCode;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Authorize]
[Route("api/source-code-providers")]
public class SourceCodeProvidersController(ISourceCodeService sourceCodeService) : SigilController
{
    [Authorize(Policy = SigilPermissions.CanAccessAdmin)]
    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await sourceCodeService.GetProvidersAsync());

    [Authorize(Policy = SigilPermissions.CanAccessAdmin)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProviderRequest request)
    {
        var userId = GetUserId() ?? Guid.Empty;
        var created = await sourceCodeService.AddProviderAsync(request, userId);
        return CreatedAtAction(nameof(List), created);
    }

    [Authorize(Policy = SigilPermissions.CanAccessAdmin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await sourceCodeService.DeleteProviderAsync(id);
        return deleted ? NoContent() : Conflict(new { error = "Provider is linked to one or more repositories." });
    }

    [Authorize(Policy = SigilPermissions.CanAccessAdmin)]
    [HttpPost("{id:int}/test")]
    public async Task<IActionResult> Test(int id, [FromBody] TestConnectionRequest request)
    {
        var ok = await sourceCodeService.TestConnectionAsync(id, request.Owner, request.Repo);
        return ok ? Ok(new { success = true }) : BadRequest(new { error = "Connection test failed. Check the token and repository details." });
    }
}

public record TestConnectionRequest(string Owner, string Repo);
