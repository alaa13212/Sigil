using Sigil.Domain.Enums;

namespace Sigil.Domain.Tests.Enums;

public class SeverityHelperTests
{
    [Theory]
    [InlineData("fatal", Severity.Fatal)]
    [InlineData("error", Severity.Error)]
    [InlineData("warning", Severity.Warning)]
    [InlineData("info", Severity.Info)]
    [InlineData("debug", Severity.Debug)]
    public void Parse_ValidString_ReturnsCorrectSeverity(string input, Severity expected)
    {
        SeverityHelper.Parse(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("FATAL", Severity.Fatal)]
    [InlineData("Error", Severity.Error)]
    [InlineData("WARNING", Severity.Warning)]
    [InlineData("Info", Severity.Info)]
    [InlineData("DEBUG", Severity.Debug)]
    public void Parse_CaseInsensitive_ReturnsCorrectSeverity(string input, Severity expected)
    {
        SeverityHelper.Parse(input).Should().Be(expected);
    }

    [Fact]
    public void Parse_Null_ReturnsError()
    {
        SeverityHelper.Parse(null).Should().Be(Severity.Error);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("critical")]
    [InlineData("trace")]
    public void Parse_UnknownString_ThrowsArgumentOutOfRangeException(string input)
    {
        var act = () => SeverityHelper.Parse(input);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(Severity.Fatal, "fatal")]
    [InlineData(Severity.Error, "error")]
    [InlineData(Severity.Warning, "warning")]
    [InlineData(Severity.Info, "info")]
    [InlineData(Severity.Debug, "debug")]
    public void ToStringValue_ValidSeverity_ReturnsCorrectString(Severity severity, string expected)
    {
        severity.ToStringValue().Should().Be(expected);
    }

    [Fact]
    public void ToStringValue_InvalidSeverity_Throws()
    {
        var act = () => ((Severity)99).ToStringValue();
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IsAbove_HigherSeverity_ReturnsTrue()
    {
        Severity.Fatal.IsAbove(Severity.Error).Should().BeTrue();
    }

    [Fact]
    public void IsAbove_LowerSeverity_ReturnsFalse()
    {
        Severity.Debug.IsAbove(Severity.Fatal).Should().BeFalse();
    }

    [Fact]
    public void IsAbove_SameSeverity_ReturnsFalse()
    {
        Severity.Error.IsAbove(Severity.Error).Should().BeFalse();
    }

    [Fact]
    public void IsAbove_FullOrderChain()
    {
        Severity.Fatal.IsAbove(Severity.Error).Should().BeTrue();
        Severity.Error.IsAbove(Severity.Warning).Should().BeTrue();
        Severity.Warning.IsAbove(Severity.Info).Should().BeTrue();
        Severity.Info.IsAbove(Severity.Debug).Should().BeTrue();
    }

    [Fact]
    public void IsAtLeast_SameSeverity_ReturnsTrue()
    {
        Severity.Error.IsAtLeast(Severity.Error).Should().BeTrue();
    }

    [Fact]
    public void IsAtLeast_HigherSeverity_ReturnsTrue()
    {
        Severity.Fatal.IsAtLeast(Severity.Warning).Should().BeTrue();
    }

    [Fact]
    public void IsAtLeast_LowerSeverity_ReturnsFalse()
    {
        Severity.Debug.IsAtLeast(Severity.Error).Should().BeFalse();
    }

    [Fact]
    public void IsAtLeast_FullOrderChain()
    {
        Severity.Fatal.IsAtLeast(Severity.Fatal).Should().BeTrue();
        Severity.Error.IsAtLeast(Severity.Error).Should().BeTrue();
        Severity.Warning.IsAtLeast(Severity.Warning).Should().BeTrue();
        Severity.Info.IsAtLeast(Severity.Info).Should().BeTrue();
        Severity.Debug.IsAtLeast(Severity.Debug).Should().BeTrue();
        Severity.Debug.IsAtLeast(Severity.Fatal).Should().BeFalse();
    }
}
