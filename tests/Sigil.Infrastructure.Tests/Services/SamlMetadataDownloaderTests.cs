using System.Net;
using System.Text;
using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class SamlMetadataDownloaderTests
{
    private const string PublicHost = "93.184.216.34";

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        new(new StubHandler(responder));

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/xml")
    };

    [Fact]
    public async Task DownloadAsync_ReturnsBody()
    {
        var client = CreateClient(_ => Ok("<EntityDescriptor />"));

        var xml = await SamlMetadataDownloader.DownloadAsync(client, $"http://{PublicHost}/metadata");

        xml.Should().Be("<EntityDescriptor />");
    }

    [Fact]
    public async Task DownloadAsync_NonHttpScheme_Throws()
    {
        var client = CreateClient(_ => Ok("<EntityDescriptor />"));

        var act = () => SamlMetadataDownloader.DownloadAsync(client, "file:///etc/passwd");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*http or https*");
    }

    [Fact]
    public async Task DownloadAsync_LoopbackHost_Throws()
    {
        var client = CreateClient(_ => Ok("<EntityDescriptor />"));

        var act = () => SamlMetadataDownloader.DownloadAsync(client, "http://127.0.0.1/metadata");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*loopback*");
    }

    [Fact]
    public async Task DownloadAsync_LinkLocalHost_Throws()
    {
        var client = CreateClient(_ => Ok("<EntityDescriptor />"));

        var act = () => SamlMetadataDownloader.DownloadAsync(client, "http://169.254.169.254/latest/meta-data");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*blocked address*");
    }

    [Fact]
    public async Task DownloadAsync_RedirectToBlockedHost_Throws()
    {
        var client = CreateClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("http://169.254.169.254/latest/meta-data");
            return response;
        });

        var act = () => SamlMetadataDownloader.DownloadAsync(client, $"http://{PublicHost}/metadata");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*blocked address*");
    }

    [Fact]
    public async Task DownloadAsync_RedirectToNonHttpScheme_Throws()
    {
        var client = CreateClient(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("file:///etc/passwd");
            return response;
        });

        var act = () => SamlMetadataDownloader.DownloadAsync(client, $"http://{PublicHost}/metadata");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*http or https*");
    }

    [Fact]
    public async Task DownloadAsync_BodyAboveSizeLimit_Throws()
    {
        var body = new string('x', SamlMetadataDownloader.MaxMetadataBytes + 1);
        var client = CreateClient(_ => Ok(body));

        var act = () => SamlMetadataDownloader.DownloadAsync(client, $"http://{PublicHost}/metadata");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*larger than*");
    }

    [Fact]
    public async Task DownloadAsync_DeclaredContentLengthAboveSizeLimit_Throws()
    {
        var client = CreateClient(_ =>
        {
            var content = new StringContent(new string('x', 16), Encoding.UTF8, "application/xml");
            content.Headers.ContentLength = SamlMetadataDownloader.MaxMetadataBytes + 1;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        var act = () => SamlMetadataDownloader.DownloadAsync(client, $"http://{PublicHost}/metadata");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*larger than*");
    }
}
