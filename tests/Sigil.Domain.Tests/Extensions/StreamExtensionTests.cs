using System.Text;
using Sigil.Domain.Extensions;

namespace Sigil.Domain.Tests.Extensions;

public class StreamExtensionTests
{
    [Fact]
    public async Task ReadAsStringAsync_Utf8Content_ReturnsString()
    {
        var content = "Hello, world!";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await stream.ReadAsStringAsync();

        result.Should().Be(content);
    }

    [Fact]
    public async Task ReadAsStringAsync_EmptyStream_ReturnsEmpty()
    {
        using var stream = new MemoryStream();

        var result = await stream.ReadAsStringAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsStringAsync_ExplicitEncoding_ReturnsString()
    {
        var content = "Héllo wörld";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await stream.ReadAsStringAsync(Encoding.UTF8);

        result.Should().Be(content);
    }
}
