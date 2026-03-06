using Sigil.Application.Services;
using Sigil.Domain.Enums;

namespace Sigil.Application.Tests.Services;

public class JavaStackFrameCleanerTests
{
    private readonly JavaStackFrameCleaner _cleaner = new();

    [Fact]
    public void Platform_IsJava()
    {
        _cleaner.Platform.Should().Be(Platform.Java);
    }

    [Theory]
    [InlineData("access$100")]
    [InlineData("access$200")]
    public void CleanMethodName_AccessBridge_ReturnsBridge(string input)
    {
        _cleaner.CleanMethodName(input).Should().Be("(bridge)");
    }

    [Theory]
    [InlineData("lambda$method$0")]
    [InlineData("lambda$$0")]
    public void CleanMethodName_Lambda_ReturnsLambda(string input)
    {
        _cleaner.CleanMethodName(input).Should().Be("(lambda)");
    }

    [Theory]
    [InlineData("main")]
    [InlineData("com.example.MyClass.run")]
    public void CleanMethodName_NormalMethod_ReturnsUnchanged(string input)
    {
        _cleaner.CleanMethodName(input).Should().Be(input);
    }
}
