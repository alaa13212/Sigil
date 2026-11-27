using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Server.Auth;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/admin/app-config")]
[Authorize(Policy = SigilPermissions.CanAccessAdmin)]
public class AppConfigController(IAppConfigEditorService appConfigEditorService) : SigilController
{
    [CookieOnly]
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await appConfigEditorService.GetAllAsync());

    [HttpPut("{key}")]
    public async Task<IActionResult> Set(string key, [FromBody] SetConfigValueRequest request)
    {
        await appConfigEditorService.SetAsync(key, request.Value);
        return NoContent();
    }
}
