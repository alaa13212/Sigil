using Microsoft.AspNetCore.Identity;

namespace Sigil.Server.Auth;

/// <summary>Selects the authentication scheme for a request.</summary>
public static class ApiTokenAuthenticationPolicy
{
    /// <summary>Name of the policy scheme that dispatches between API tokens and the UI cookie.</summary>
    public const string SchemeName = "SigilApiTokenOrCookie";

    private const string BearerPrefix = "Bearer ";

    /// <summary>Returns the scheme that authenticates the request.</summary>
    public static string SelectScheme(HttpContext context)
    {
        return UsesApiToken(context) ? ApiTokenAuthenticationHandler.SchemeName : IdentityConstants.ApplicationScheme;
    }

    /// <summary>Returns true when the request is a read-only API call carrying a bearer token.</summary>
    public static bool UsesApiToken(HttpContext context)
    {
        if (!IsReadMethod(context.Request.Method))
            return false;

        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        if (IsCookieOnly(context))
            return false;

        return ReadBearerToken(context.Request) is not null;
    }

    /// <summary>Returns true when the endpoint that handles the request is reserved for the interface session.</summary>
    public static bool IsCookieOnly(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<CookieOnlyAttribute>() is not null;

    /// <summary>Reads the bearer token from the authorization header, or null when there is none.</summary>
    public static string? ReadBearerToken(HttpRequest request)
    {
        string header = request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(header))
            return null;

        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        string token = header[BearerPrefix.Length..].Trim();
        return token.Length == 0 ? null : token;
    }

    private static bool IsReadMethod(string method)
        => HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
}
