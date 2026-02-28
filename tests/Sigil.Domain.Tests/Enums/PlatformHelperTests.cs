using Sigil.Domain.Enums;

namespace Sigil.Domain.Tests.Enums;

public class PlatformHelperTests
{
    [Theory]
    [InlineData("csharp", Platform.CSharp)]
    [InlineData("javascript", Platform.JavaScript)]
    [InlineData("python", Platform.Python)]
    [InlineData("java", Platform.Java)]
    [InlineData("ruby", Platform.Ruby)]
    [InlineData("php", Platform.PHP)]
    [InlineData("go", Platform.Go)]
    [InlineData("node", Platform.Node)]
    [InlineData("elixir", Platform.Elixir)]
    [InlineData("perl", Platform.Perl)]
    [InlineData("as3", Platform.ActionScript3)]
    [InlineData("c", Platform.C)]
    [InlineData("cfml", Platform.ColdFusion)]
    [InlineData("cocoa", Platform.Cocoa)]
    [InlineData("haskell", Platform.Haskell)]
    [InlineData("groovy", Platform.Groovy)]
    [InlineData("native", Platform.Native)]
    [InlineData("objc", Platform.ObjectiveC)]
    [InlineData("other", Platform.Other)]
    public void Parse_KnownPlatform_ReturnsCorrectEnum(string input, Platform expected)
    {
        PlatformHelper.Parse(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("CSHARP")]
    [InlineData("JavaScript")]
    [InlineData("PYTHON")]
    public void Parse_KnownPlatformDifferentCase_ReturnsCorrectEnum(string input)
    {
        var result = PlatformHelper.Parse(input);
        result.Should().NotBe(Platform.Other);
    }

    [Theory]
    [InlineData("unknown-sdk")]
    [InlineData("dotnet")]
    [InlineData("")]
    [InlineData("react-native")]
    [InlineData("flutter")]
    public void Parse_UnknownPlatform_ReturnsOther(string input)
    {
        PlatformHelper.Parse(input).Should().Be(Platform.Other);
    }

    [Theory]
    [InlineData(Platform.CSharp, "csharp")]
    [InlineData(Platform.JavaScript, "javascript")]
    [InlineData(Platform.Python, "python")]
    [InlineData(Platform.Other, "other")]
    public void ToStringValue_KnownPlatform_ReturnsExpectedString(Platform platform, string expected)
    {
        PlatformHelper.ToStringValue(platform).Should().Be(expected);
    }

    [Theory]
    [InlineData(Platform.CSharp)]
    [InlineData(Platform.JavaScript)]
    [InlineData(Platform.Python)]
    [InlineData(Platform.Java)]
    [InlineData(Platform.Ruby)]
    [InlineData(Platform.Node)]
    [InlineData(Platform.Go)]
    [InlineData(Platform.PHP)]
    public void RoundTrip_ParseThenToString_ReturnsSameValue(Platform platform)
    {
        var str = PlatformHelper.ToStringValue(platform);
        var parsed = PlatformHelper.Parse(str);
        parsed.Should().Be(platform);
    }
}
