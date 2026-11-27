using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Sigil.Infrastructure.Services;

internal static class SamlMetadataDownloader
{
    internal const int MaxMetadataBytes = 1_048_576;
    internal const string ClientName = "saml-metadata";

    private const int MaxRedirects = 5;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public static async Task<string> DownloadAsync(HttpClient client, string metadataUrl)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataUrl);

        var uri = ValidateUri(metadataUrl);

        using var timeout = new CancellationTokenSource(Timeout);

        for (var redirect = 0; ; redirect++)
        {
            await EnsureAllowedHostAsync(uri, timeout.Token);

            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (IsRedirect(response.StatusCode) && response.Headers.Location is not null)
            {
                if (redirect >= MaxRedirects)
                    throw new InvalidOperationException("The metadata URL redirected too many times.");

                uri = ValidateUri(new Uri(uri, response.Headers.Location).AbsoluteUri);
                continue;
            }

            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is > MaxMetadataBytes)
                throw new InvalidOperationException(
                    $"The metadata document is larger than the {MaxMetadataBytes} byte limit.");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            return await ReadLimitedAsync(stream, timeout.Token);
        }
    }

    private static Uri ValidateUri(string metadataUrl)
    {
        if (!Uri.TryCreate(metadataUrl, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("The metadata URL is not a valid absolute URL.");

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The metadata URL must use the http or https scheme.");

        return uri;
    }

    private static async Task EnsureAllowedHostAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (uri.IsLoopback)
            throw new InvalidOperationException("The metadata URL must not point at the loopback interface.");

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"The metadata host '{uri.Host}' could not be resolved.", ex);
        }

        if (addresses.Length == 0)
            throw new InvalidOperationException($"The metadata host '{uri.Host}' could not be resolved.");

        foreach (var address in addresses)
        {
            if (IsBlocked(address))
                throw new InvalidOperationException(
                    $"The metadata host '{uri.Host}' resolves to the blocked address '{address}'.");
        }
    }

    private static bool IsBlocked(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address)) return true;
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return true;
        if (address.IsIPv6LinkLocal) return true;
        if (address.IsIPv6Multicast) return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();

            if (bytes[0] == 169 && bytes[1] == 254) return true;
            if (bytes[0] >= 224 && bytes[0] <= 239) return true;
        }

        return false;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static async Task<string> ReadLimitedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];

        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxMetadataBytes)
                throw new InvalidOperationException(
                    $"The metadata document is larger than the {MaxMetadataBytes} byte limit.");

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
