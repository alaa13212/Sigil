using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Saml;
using Sigil.Domain.Entities;
using Sigil.Server.Auth.Saml;

namespace Sigil.Server.Controllers;

[AllowAnonymous]
public class SamlController(
    ISamlConnectionService connectionService,
    ISamlLoginService loginService,
    SamlConnectionRegistry registry,
    SignInManager<User> signInManager,
    UserManager<User> userManager,
    ILogger<SamlController> logger) : Controller
{
    [HttpGet("/saml/login/{connectionId:guid}")]
    public async Task<IActionResult> Login(Guid connectionId, [FromQuery] string? returnUrl)
    {
        var connection = await connectionService.GetConnectionAsync(connectionId);
        if (connection is null || !connection.IsEnabled)
            return NotFound();

        if (!registry.Current.Connections.Any(c => c.Id == connectionId))
        {
            return Content(
                RenderMessage(
                    "Sign-in is not available",
                    "This sign-in connection has no usable identity provider metadata."),
                "text/html");
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = SamlConstants.CompletePath,
            Items =
            {
                ["idp"] = connection.EntityId,
                ["connectionId"] = connectionId.ToString(),
                ["returnUrl"] = SanitizeReturnUrl(returnUrl)
            }
        };

        return Challenge(properties, SamlConstants.Scheme);
    }

    [HttpGet("/saml/complete")]
    public async Task<IActionResult> Complete()
    {
        var result = await HttpContext.AuthenticateAsync(SamlConstants.ExternalScheme);
        if (!result.Succeeded || result.Principal is null)
            return Redirect("/login?error=saml");

        var items = result.Properties?.Items;
        var connectionId = ParseGuid(items, "connectionId");
        if (connectionId is null)
            return Redirect("/login?error=saml");

        var returnUrl = SanitizeReturnUrl(ParseString(items, "returnUrl"));

        var outcome = await loginService.CompleteLoginAsync(connectionId.Value, new SamlAssertion(CollectClaims(result.Principal)));

        switch (outcome.Status)
        {
            case SamlLoginStatus.Approved:
                var user = await userManager.FindByIdAsync(outcome.UserId!.Value.ToString());
                if (user is null)
                {
                    await HttpContext.SignOutAsync(SamlConstants.ExternalScheme);
                    return Redirect("/login?error=saml");
                }

                await signInManager.SignInAsync(user, isPersistent: true);
                await HttpContext.SignOutAsync(SamlConstants.ExternalScheme);
                return Redirect(returnUrl);

            case SamlLoginStatus.Pending:
                await HttpContext.SignOutAsync(SamlConstants.ExternalScheme);
                return Content(
                    RenderMessage(
                        "Waiting for approval",
                        outcome.Message ?? "Your account is waiting for an administrator to approve it."),
                    "text/html");

            case SamlLoginStatus.Rejected:
                await HttpContext.SignOutAsync(SamlConstants.ExternalScheme);
                return Content(
                    RenderMessage(
                        "Access denied",
                        outcome.Message ?? "Access to Sigil has been denied for this account."),
                    "text/html");

            default:
                logger.LogWarning("A SAML login could not be completed: {Message}", outcome.Message);
                await HttpContext.SignOutAsync(SamlConstants.ExternalScheme);
                return Redirect("/login?error=saml");
        }
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> CollectClaims(ClaimsPrincipal principal)
    {
        var claims = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var claim in principal.Claims)
        {
            if (!claims.TryGetValue(claim.Type, out var values))
            {
                values = [];
                claims[claim.Type] = values;
            }

            values.Add(claim.Value);
        }

        return claims.ToDictionary(k => k.Key, v => (IReadOnlyList<string>)v.Value, StringComparer.Ordinal);
    }

    private static string? ParseString(IDictionary<string, string?>? items, string key) =>
        items is not null && items.TryGetValue(key, out var value) ? value : null;

    private static Guid? ParseGuid(IDictionary<string, string?>? items, string key) =>
        Guid.TryParse(ParseString(items, key), out var value) ? value : null;

    private static string SanitizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return "/";
        if (!returnUrl.StartsWith('/')) return "/";
        if (returnUrl.StartsWith("//") || returnUrl.StartsWith("/\\")) return "/";

        return returnUrl;
    }

    private static string RenderMessage(string title, string message)
    {
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        html.Append($"<title>{WebUtility.HtmlEncode(title)} — Sigil</title></head>");
        html.Append("<body style=\"margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;");
        html.Append("font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:#1d232a;color:#e5e7eb;\">");
        html.Append("<main style=\"max-width:28rem;padding:2rem;background:#282c34;border-radius:0.5rem;");
        html.Append("box-shadow:0 10px 25px rgba(0,0,0,0.35);\">");
        html.Append($"<h1 style=\"margin:0 0 0.75rem;font-size:1.25rem;\">{WebUtility.HtmlEncode(title)}</h1>");
        html.Append($"<p style=\"margin:0 0 1.5rem;line-height:1.5;color:#c3c8cf;\">{WebUtility.HtmlEncode(message)}</p>");
        html.Append("<a href=\"/login\" style=\"display:inline-block;padding:0.5rem 1rem;border-radius:0.375rem;");
        html.Append("background:#3b82f6;color:#ffffff;text-decoration:none;\">Back to sign in</a>");
        html.Append("</main></body></html>");

        return html.ToString();
    }
}
