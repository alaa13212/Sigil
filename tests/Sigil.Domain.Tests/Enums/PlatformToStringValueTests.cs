using Sigil.Domain.Enums;

namespace Sigil.Domain.Tests.Enums;

public class PlatformToStringValueTests
{
    [Theory]
    [InlineData(Platform.ActionScript3, "as3")]
    [InlineData(Platform.C, "c")]
    [InlineData(Platform.ColdFusion, "cfml")]
    [InlineData(Platform.Cocoa, "cocoa")]
    [InlineData(Platform.Elixir, "elixir")]
    [InlineData(Platform.Haskell, "haskell")]
    [InlineData(Platform.Groovy, "groovy")]
    [InlineData(Platform.Native, "native")]
    [InlineData(Platform.ObjectiveC, "objc")]
    public void ToStringValue_ReturnsExpectedString(Platform platform, string expected)
    {
        PlatformHelper.ToStringValue(platform).Should().Be(expected);
    }

    [Fact]
    public void ToStringValue_UnknownValue_ThrowsWithPlatformInMessage()
    {
        var unknown = (Platform)999;
        var act = () => PlatformHelper.ToStringValue(unknown);
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*999*");
    }
}
