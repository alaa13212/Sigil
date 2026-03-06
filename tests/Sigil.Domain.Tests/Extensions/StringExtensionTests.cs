using Sigil.Domain.Extensions;

namespace Sigil.Domain.Tests.Extensions;

public class StringExtensionTests
{
    [Theory]
    [InlineData("ParsedEvent", "Parsed Event")]
    [InlineData("alreadylower", "alreadylower")]
    [InlineData("A", "A")]
    [InlineData("", "")]
    public void SplitPascal_ReturnsExpected(string input, string expected)
    {
        input.SplitPascal().Should().Be(expected);
    }

    [Fact]
    public void SplitPascal_ConsecutiveUppercase_OnlySplitsLowercaseToUppercase()
    {
        // The regex [a-z][A-Z] only inserts space at lowercase→uppercase boundaries
        "MyHTTPClient".SplitPascal().Should().Be("My HTTPClient");
    }

    [Fact]
    public void SplitPascal_AllUppercase_ReturnsUnchanged()
    {
        "ABC".SplitPascal().Should().Be("ABC");
    }

    [Fact]
    public void Truncate_ShorterThanMax_ReturnsOriginal()
    {
        "hello".Truncate(10).Should().Be("hello");
    }

    [Fact]
    public void Truncate_ExactLength_ReturnsOriginal()
    {
        "hello".Truncate(5).Should().Be("hello");
    }

    [Fact]
    public void Truncate_LongerThanMax_ReturnsTruncated()
    {
        "hello world".Truncate(5).Should().Be("hello");
    }

    [Fact]
    public void Truncate_ZeroMax_ReturnsEmpty()
    {
        "hello".Truncate(0).Should().Be("");
    }

    [Fact]
    public void Truncate_EmptyString_ReturnsEmpty()
    {
        "".Truncate(5).Should().Be("");
    }
}
