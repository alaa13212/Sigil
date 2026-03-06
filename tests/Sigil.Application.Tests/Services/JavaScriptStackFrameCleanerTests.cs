using Sigil.Application.Services;
using Sigil.Domain.Enums;

namespace Sigil.Application.Tests.Services;

public class JavaScriptStackFrameCleanerTests
{
    private readonly JavaScriptStackFrameCleaner _cleaner = new();

    [Fact]
    public void Platform_IsJavaScript()
    {
        _cleaner.Platform.Should().Be(Platform.JavaScript);
    }

    [Theory]
    [InlineData("<anonymous>")]
    [InlineData("anonymous")]
    public void CleanMethodName_Anonymous_ReturnsAnonymous(string input)
    {
        _cleaner.CleanMethodName(input).Should().Be("(anonymous)");
    }

    [Fact]
    public void CleanMethodName_WebpackPath_ReturnsWebpackModule()
    {
        _cleaner.CleanMethodName("webpack:///./src/App.js").Should().Be("(webpack module)");
    }

    [Theory]
    [InlineData("handleClick")]
    [InlineData("module.exports")]
    public void CleanMethodName_NormalMethod_ReturnsUnchanged(string input)
    {
        _cleaner.CleanMethodName(input).Should().Be(input);
    }
}
