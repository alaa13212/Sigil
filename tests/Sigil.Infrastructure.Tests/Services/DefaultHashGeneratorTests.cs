using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class DefaultHashGeneratorTests
{
    private readonly DefaultHashGenerator _generator = new();

    [Fact]
    public void ComputeHash_Deterministic_SameInputSameOutput()
    {
        var hash1 = _generator.ComputeHash("hello");
        var hash2 = _generator.ComputeHash("hello");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentInputs_DifferentOutputs()
    {
        var hash1 = _generator.ComputeHash("hello");
        var hash2 = _generator.ComputeHash("world");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsUppercaseHexString()
    {
        var hash = _generator.ComputeHash("test");

        hash.Should().MatchRegex("^[0-9A-F]+$");
    }

    [Fact]
    public void ComputeHash_NonEmptyResult()
    {
        var hash = _generator.ComputeHash("anything");

        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ComputeHash_Sha256Length_Returns64Chars()
    {
        var hash = _generator.ComputeHash("test");

        hash.Should().HaveLength(64); // SHA-256 = 32 bytes = 64 hex chars
    }
}
