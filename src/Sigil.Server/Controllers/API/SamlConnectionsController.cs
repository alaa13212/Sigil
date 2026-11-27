using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Saml;
using Sigil.Server.Auth.Saml;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/saml/connections")]
public class SamlConnectionsController(
    ISamlConnectionService connectionService,
    SamlConnectionRegistry registry,
    ILogger<SamlConnectionsController> logger) : SigilController
{
    [AllowAnonymous]
    [HttpGet("available")]
    public async Task<IActionResult> Available()
        => Ok(await connectionService.GetEnabledConnectionsAsync());

    [Authorize(Policy = SigilPermissions.CanAccessAdmin)]
    [HttpGet]
    public async Task<IActionResult> List()
        => Ok(await connectionService.GetConnectionsAsync());

    [Authorize(Policy = SigilPermissions.CanAccessAdmin)]
    [HttpGet("endpoints")]
    public async Task<IActionResult> Endpoints()
        => Ok(await connectionService.GetEndpointsAsync());

    [Authorize(Policy = SigilPermissions.CanAccessAdmin)]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SamlConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.EntityId))
            return BadRequest(new { error = "A name and an identity provider entity id are required." });

        var created = await connectionService.CreateConnectionAsync(request);

        await RefreshMetadataIfConfigured(created);
        await registry.RefreshAsync();

        return CreatedAtAction(nameof(List), created);
    }

    [Authorize(Policy = SigilPermissions.CanAccessAdmin)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SamlConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.EntityId))
            return BadRequest(new { error = "A name and an identity provider entity id are required." });

        var updated = await connectionService.UpdateConnectionAsync(id, request);
        if (updated is null) return NotFound();

        await RefreshMetadataIfConfigured(updated);
        await registry.RefreshAsync();

        return Ok(updated);
    }

    [Authorize(Policy = SigilPermissions.CanAccessAdmin)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await connectionService.DeleteConnectionAsync(id);
        if (!deleted) return NotFound();

        await registry.RefreshAsync();

        return NoContent();
    }

    [Authorize(Policy = SigilPermissions.CanAccessAdmin)]
    [HttpPost("{id:guid}/refresh-metadata")]
    public async Task<IActionResult> RefreshMetadata(Guid id)
    {
        var connection = await connectionService.GetConnectionAsync(id);
        if (connection is null) return NotFound();

        if (string.IsNullOrWhiteSpace(connection.MetadataUrl))
            return BadRequest(new { error = "This connection has no metadata url." });

        var refreshed = await connectionService.RefreshMetadataAsync(id);
        await registry.RefreshAsync();

        var result = await connectionService.GetConnectionAsync(id);

        if (!refreshed)
        {
            return BadRequest(new { error = result?.MetadataError ?? "The metadata could not be loaded." });
        }

        return Ok(result);
    }

    private async Task RefreshMetadataIfConfigured(SamlConnectionResponse connection)
    {
        if (string.IsNullOrWhiteSpace(connection.MetadataUrl)) return;

        var refreshed = await connectionService.RefreshMetadataAsync(connection.Id);

        if (!refreshed)
        {
            logger.LogWarning("The metadata for SAML connection {EntityId} could not be loaded.", connection.EntityId);
        }
    }
}
