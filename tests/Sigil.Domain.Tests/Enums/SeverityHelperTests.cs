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
    public void IsMoreSevereThan_FatalMoreSevereThanError_ReturnsTrue()
    {
        Severity.Fatal.IsMoreSevereThan(Severity.Error).Should().BeTrue();
    }

    [Fact]
    public void IsMoreSevereThan_DebugLessSevereThanFatal_ReturnsFalse()
    {
        Severity.Debug.IsMoreSevereThan(Severity.Fatal).Should().BeFalse();
    }

    [Fact]
    public void IsMoreSevereThan_SameSeverity_ReturnsFalse()
    {
        Severity.Error.IsMoreSevereThan(Severity.Error).Should().BeFalse();
    }

    [Fact]
    public void IsMoreSevereThan_FullOrderChain()
    {
        Severity.Fatal.IsMoreSevereThan(Severity.Error).Should().BeTrue();
        Severity.Error.IsMoreSevereThan(Severity.Warning).Should().BeTrue();
        Severity.Warning.IsMoreSevereThan(Severity.Info).Should().BeTrue();
        Severity.Info.IsMoreSevereThan(Severity.Debug).Should().BeTrue();
    }
}
