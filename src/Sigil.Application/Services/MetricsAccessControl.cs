using System.Net;
using System.Security.Cryptography;
using System.Text;
using Sigil.Domain;

namespace Sigil.Application.Services;

/// <summary>Access decision for a single scrape of the metrics endpoint.</summary>
public enum MetricsAccess
{
    /// <summary>The request may read the metrics.</summary>
    Allowed,

    /// <summary>The endpoint has neither a token nor an allow-list configured.</summary>
    NotConfigured,

    /// <summary>No credential was presented and the request is not from an allowed network.</summary>
    MissingCredentials,

    /// <summary>A credential was presented but did not match.</summary>
    InvalidCredentials,
}

/// <summary>Decides whether a metrics scrape is authorized. Never consults UI cookie authentication.</summary>
public static class MetricsAccessControl
{
    /// <summary>Header carrying a bearer token.</summary>
    public const string AuthorizationHeader = "Authorization";

    /// <summary>Query string parameter carrying a token.</summary>
    public const string TokenQueryParameter = "token";

    private const string BearerPrefix = "Bearer ";

    /// <summary>Extracts the presented token from an authorization header value or query string token.</summary>
    public static string? ExtractToken(string? authorizationHeader, string? queryToken)
    {
        if (!string.IsNullOrWhiteSpace(authorizationHeader))
        {
            string header = authorizationHeader.Trim();
            if (header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string bearer = header[BearerPrefix.Length..].Trim();
                if (bearer.Length > 0)
                    return bearer;
            }
        }

        return string.IsNullOrWhiteSpace(queryToken) ? null : queryToken.Trim();
    }

    /// <summary>Compares two secrets without leaking their contents through timing.</summary>
    public static bool TokensMatch(string? expected, string? presented)
    {
        if (string.IsNullOrEmpty(expected) || presented is null)
            return false;

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] presentedBytes = Encoding.UTF8.GetBytes(presented);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, presentedBytes);
    }

    /// <summary>Parses a comma or semicolon separated CIDR list, ignoring unparsable entries.</summary>
    public static IReadOnlyList<IPNetwork> ParseAllowedCidrs(string? allowedCidrs)
    {
        if (string.IsNullOrWhiteSpace(allowedCidrs))
            return [];

        var networks = new List<IPNetwork>();
        foreach (string entry in allowedCidrs.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IPNetwork.TryParse(entry, out IPNetwork network))
                networks.Add(network);
        }

        return networks;
    }

    /// <summary>Returns true when the address falls inside any of the allowed networks.</summary>
    public static bool IsAddressAllowed(string? remoteAddress, IReadOnlyList<IPNetwork> allowedNetworks)
    {
        if (allowedNetworks.Count == 0 || string.IsNullOrWhiteSpace(remoteAddress))
            return false;

        if (!IPAddress.TryParse(remoteAddress.Trim(), out IPAddress? address))
            return false;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        foreach (IPNetwork network in allowedNetworks)
        {
            if (Contains(network, address))
                return true;
        }

        return false;
    }

    /// <summary>Applies the token and CIDR rules to one scrape request.</summary>
    public static MetricsAccess Evaluate(string? configuredToken, string? allowedCidrs, string? presentedToken, string? remoteAddress)
    {
        bool hasToken = !string.IsNullOrWhiteSpace(configuredToken);
        IReadOnlyList<IPNetwork> networks = ParseAllowedCidrs(allowedCidrs);

        if (!hasToken && networks.Count == 0)
            return MetricsAccess.NotConfigured;

        if (IsAddressAllowed(remoteAddress, networks))
            return MetricsAccess.Allowed;

        if (!hasToken)
            return presentedToken is null ? MetricsAccess.MissingCredentials : MetricsAccess.InvalidCredentials;

        if (presentedToken is null)
            return MetricsAccess.MissingCredentials;

        return TokensMatch(configuredToken, presentedToken) ? MetricsAccess.Allowed : MetricsAccess.InvalidCredentials;
    }

    /// <summary>Maps an access decision to the HTTP status code returned by the endpoint.</summary>
    public static int ToStatusCode(MetricsAccess access) => access switch
    {
        MetricsAccess.Allowed => 200,
        MetricsAccess.MissingCredentials => 401,
        _ => 403
    };

    private static bool Contains(IPNetwork network, IPAddress address)
    {
        IPAddress? prefix = network.BaseAddress;
        if (prefix is null)
            return false;

        if (prefix.AddressFamily != address.AddressFamily)
            return false;

        return network.Contains(address);
    }
}
