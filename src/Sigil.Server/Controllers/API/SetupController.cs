using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/setup")]
[AllowAnonymous]
public class SetupController(ISetupService setupService) : SigilController
{
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        return Ok(await setupService.GetSetupStatusAsync());
    }

    [HttpGet("db-status")]
    public async Task<IActionResult> DbStatus()
    {
        return Ok(await setupService.GetDbStatusAsync());
    }

    [HttpPost("migrate")]
    public async Task<IActionResult> Migrate()
    {
        var migrated = await setupService.MigrateAsync();
        return migrated ? Ok(new { success = true }) : BadRequest(new { error = "Setup is already complete." });
    }

    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize([FromBody] SetupRequest request)
    {
        var result = await setupService.InitializeAsync(request);
        return result.Succeeded ? Ok(result) : BadRequest(new { errors = result.Errors });
    }
}
