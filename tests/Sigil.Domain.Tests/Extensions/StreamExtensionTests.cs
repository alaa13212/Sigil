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

    [Fact]
    public async Task ReadAsStringAsync_ExplicitLatin1Encoding_UsesProvidedEncodingNotUtf8()
    {
        // 'é' is 0xE9 in Latin1; that byte is invalid in UTF-8, so using UTF-8 would return a replacement char
        var content = "caf\u00E9";
        using var stream = new MemoryStream(Encoding.Latin1.GetBytes(content));

        var result = await stream.ReadAsStringAsync(Encoding.Latin1);

        result.Should().Be(content);
    }

    [Fact]
    public async Task ReadAsStringAsync_StreamWithUtf16Bom_DetectsEncodingFromByteOrderMark()
    {
        // UTF-16 LE BOM + UTF-16 LE content; without BOM detection this reads as garbled UTF-8
        var content = "Hello BOM";
        var bytes = new byte[] { 0xFF, 0xFE }.Concat(Encoding.Unicode.GetBytes(content)).ToArray();
        using var stream = new MemoryStream(bytes);

        var result = await stream.ReadAsStringAsync();

        result.Should().Be(content);
    }
}
