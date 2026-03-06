using Sigil.Application.Services;
using Sigil.Domain.Enums;

namespace Sigil.Application.Tests.Services;

public class CSharpStackFrameCleanerTests
{
    private readonly CSharpStackFrameCleaner _cleaner = new();

    [Fact]
    public void Platform_IsCSharp()
    {
        _cleaner.Platform.Should().Be(Platform.CSharp);
    }

    [Theory]
    [InlineData("<DoSomething>d__5.MoveNext", "DoSomething (async)")]
    [InlineData("<ProcessAsync>d__12.MoveNext", "ProcessAsync (async)")]
    public void CleanMethodName_AsyncStateMachine_ReturnsAsync(string input, string expected)
    {
        _cleaner.CleanMethodName(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("<>b__0_0", "(lambda)")]
    [InlineData("<Method>b__1", "(lambda)")]
    public void CleanMethodName_Lambda_ReturnsLambda(string input, string expected)
    {
        _cleaner.CleanMethodName(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("<>c__DisplayClass0_0", "(closure)")]
    [InlineData("<>c__DisplayClass12", "(closure)")]
    public void CleanMethodName_Closure_ReturnsClosure(string input, string expected)
    {
        _cleaner.CleanMethodName(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("List`1", "List")]
    [InlineData("Dictionary`2", "Dictionary")]
    public void CleanMethodName_GenericArity_RemovesBacktick(string input, string expected)
    {
        _cleaner.CleanMethodName(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Main")]
    [InlineData("MyClass.Execute")]
    public void CleanMethodName_NormalMethod_ReturnsUnchanged(string input)
    {
        _cleaner.CleanMethodName(input).Should().Be(input);
    }
}
