using Sigil.Application.Services;
using Sigil.Domain.Enums;

namespace Sigil.Application.Tests.Services;

public class PythonStackFrameCleanerTests
{
    private readonly PythonStackFrameCleaner _cleaner = new();

    [Fact]
    public void Platform_IsPython()
    {
        _cleaner.Platform.Should().Be(Platform.Python);
    }

    [Fact]
    public void CleanMethodName_Module_ReturnsModule()
    {
        _cleaner.CleanMethodName("<module>").Should().Be("(module)");
    }

    [Fact]
    public void CleanMethodName_Lambda_ReturnsLambda()
    {
        _cleaner.CleanMethodName("<lambda>").Should().Be("(lambda)");
    }

    [Theory]
    [InlineData("<listcomp>")]
    [InlineData("<dictcomp>")]
    [InlineData("<setcomp>")]
    [InlineData("<genexpr>")]
    public void CleanMethodName_Comprehension_ReturnsComprehension(string input)
    {
        _cleaner.CleanMethodName(input).Should().Be("(comprehension)");
    }

    [Theory]
    [InlineData("main")]
    [InlineData("my_function")]
    public void CleanMethodName_NormalMethod_ReturnsUnchanged(string input)
    {
        _cleaner.CleanMethodName(input).Should().Be(input);
    }
}
