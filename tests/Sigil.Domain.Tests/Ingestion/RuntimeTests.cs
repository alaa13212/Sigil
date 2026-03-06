using Sigil.Domain.Ingestion;

namespace Sigil.Domain.Tests.Ingestion;

public class RuntimeTests
{
    [Fact]
    public void ToString_ReturnsNameSpaceVersion()
    {
        var runtime = new Runtime(".NET", "8.0.0");

        runtime.ToString().Should().Be(".NET 8.0.0");
    }

    [Fact]
    public void ToString_EmptyValues_ReturnsSpaceSeparated()
    {
        var runtime = new Runtime("", "");

        runtime.ToString().Should().Be(" ");
    }

    [Fact]
    public void ToString_NameOnly_ReturnsNameWithTrailingSpace()
    {
        var runtime = new Runtime(".NET", "");

        runtime.ToString().Should().Be(".NET ");
    }

    [Fact]
    public void ToString_VersionOnly_ReturnsVersionWithLeadingSpace()
    {
        var runtime = new Runtime("", "8.0.0");

        runtime.ToString().Should().Be(" 8.0.0");
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new Runtime(".NET", "8.0.0");
        var b = new Runtime(".NET", "8.0.0");

        a.Should().Be(b);
    }
}
