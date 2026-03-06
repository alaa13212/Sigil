using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class GzipCompressionServiceTests
{
    private readonly GzipCompressionService _service = new();

    [Fact]
    public void IsCompressed_GzipMagicBytes_ReturnsTrue()
    {
        var compressed = _service.CompressString("test data");

        _service.IsCompressed(compressed).Should().BeTrue();
    }

    [Fact]
    public void IsCompressed_NonGzipData_ReturnsFalse()
    {
        var data = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        _service.IsCompressed(data).Should().BeFalse();
    }

    [Fact]
    public void IsCompressed_EmptyArray_ReturnsFalse()
    {
        _service.IsCompressed([]).Should().BeFalse();
    }

    [Fact]
    public void CompressAndDecompress_Roundtrip_ReturnsOriginal()
    {
        var original = "Hello, world! This is a test of gzip compression.";

        var compressed = _service.CompressString(original);
        var decompressed = _service.DecompressToString(compressed);

        decompressed.Should().Be(original);
    }

    [Fact]
    public void CompressAndDecompress_EmptyString_Roundtrip()
    {
        var compressed = _service.CompressString("");
        var decompressed = _service.DecompressToString(compressed);

        decompressed.Should().Be("");
    }

    [Fact]
    public void CompressString_ProducesCompressedOutput()
    {
        var compressed = _service.CompressString("test");

        compressed.Should().NotBeEmpty();
        compressed[0].Should().Be(0x1F);
        compressed[1].Should().Be(0x8B);
    }
}
