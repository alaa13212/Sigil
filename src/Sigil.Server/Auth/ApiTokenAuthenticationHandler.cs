using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.ApiTokens;

namespace Sigil.Server.Auth;

/// <summary>Authenticates a request from an API token presented as a bearer token.</summary>
public class ApiTokenAuthenticationHandler(
    IOptionsMonitor<ApiTokenAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<ApiTokenAuthenticationOptions>(options, logger, encoder)
{
    /// <summary>Name of the authentication scheme that resolves API tokens.</summary>
    public const string SchemeName = "ApiToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? presented = ApiTokenAuthenticationPolicy.ReadBearerToken(Request);
        if (presented is null)
            return AuthenticateResult.NoResult();

        IApiTokenService tokenService = Context.RequestServices.GetRequiredService<IApiTokenService>();
        ApiTokenIdentity? identity = await tokenService.AuthenticateAsync(presented);
        if (identity is null)
            return AuthenticateResult.Fail("The API token is not valid.");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.UserId.ToString()),
            new(ClaimTypes.Name, identity.Email),
            new(ClaimTypes.Email, identity.Email)
        };
        claims.AddRange(identity.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, ClaimTypes.Role));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
