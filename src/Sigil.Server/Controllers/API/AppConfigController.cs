using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Domain;
using Sigil.Server.Auth;
using Sigil.Server.Auth.Saml;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/admin/app-config")]
[Authorize(Policy = SigilPermissions.CanAccessAdmin)]
public class AppConfigController(
    IAppConfigEditorService appConfigEditorService,
    Saml2OptionsCache samlOptionsCache) : SigilController
{
    [CookieOnly]
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await appConfigEditorService.GetAllAsync());

    [HttpPut("{key}")]
    public async Task<IActionResult> Set(string key, [FromBody] SetConfigValueRequest request)
    {
        await appConfigEditorService.SetAsync(key, request.Value);

        if (key == AppConfigKeys.HostUrl)
        {
            samlOptionsCache.Invalidate();
        }

        return NoContent();
    }
}
