namespace Sigil.Domain.Cors;

public static class CorsOrigins
{
    public const string AnyOrigin = "*";

    private static readonly char[] EntrySeparators = [',', '\n', '\r'];

    public static string? Normalize(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return null;

        string value = origin.Trim();

        if (value == AnyOrigin)
            return AnyOrigin;

        if (value.Contains(',') || value.Any(char.IsWhiteSpace))
            return null;

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            return null;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return null;

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return null;

        if (uri.AbsolutePath.TrimEnd('/').Length > 0)
            return null;

        string host = uri.HostNameType == UriHostNameType.IPv6
            ? "[" + uri.IdnHost + "]"
            : uri.IdnHost;

        if (string.IsNullOrEmpty(host))
            return null;

        return uri.IsDefaultPort
            ? $"{uri.Scheme}://{host}"
            : $"{uri.Scheme}://{host}:{uri.Port}";
    }

    public static IReadOnlyList<string> Parse(string? storedValue)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
            return [];

        List<string> origins = [];

        foreach (string part in storedValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string? normalized = Normalize(part);

            if (normalized is not null && !origins.Contains(normalized, StringComparer.Ordinal))
                origins.Add(normalized);
        }

        return origins;
    }

    public static IReadOnlyList<string> Split(IEnumerable<string?>? values)
    {
        if (values is null)
            return [];

        List<string> parts = [];

        foreach (string? value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            foreach (string part in value.Split(EntrySeparators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                parts.Add(part);
        }

        return parts;
    }

    public static bool IsMatch(IReadOnlyList<string> allowedOrigins, string? origin)
    {
        if (allowedOrigins is null || allowedOrigins.Count == 0)
            return false;

        string? normalized = Normalize(origin);

        if (normalized is null)
            return false;

        if (allowedOrigins.Contains(AnyOrigin, StringComparer.Ordinal))
            return true;

        return allowedOrigins.Contains(normalized, StringComparer.Ordinal);
    }
}
