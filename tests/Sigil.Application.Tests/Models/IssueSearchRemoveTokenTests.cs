using Sigil.Application.Models.Issues;

namespace Sigil.Application.Tests.Models;

public class IssueSearchRemoveTokenTests
{
    [Fact]
    public void RemoveToken_FromMixedQuery_LeavesFreeTextAndOtherTokensIntact()
    {
        var result = IssueSearchParser.RemoveToken("crash env:prod timeout", "env", "prod");

        result.Should().Be("crash timeout");
    }

    [Fact]
    public void RemoveToken_KeepsEveryOtherToken()
    {
        var result = IssueSearchParser.RemoveToken("env:prod release:1.0 region:eu crash", "release", "1.0");

        result.Should().Be("env:prod region:eu crash");
    }

    [Fact]
    public void RemoveToken_TokenSurroundedByFreeText_KeepsBothWords()
    {
        var result = IssueSearchParser.RemoveToken("env:prod crash timeout region:eu", "env", "prod");

        result.Should().Be("region:eu crash timeout");
    }

    [Fact]
    public void RemoveToken_OnlyToken_LeavesTheFreeText()
    {
        IssueSearchParser.RemoveToken("env:prod crash", "env", "prod").Should().Be("crash");
    }

    [Fact]
    public void RemoveToken_OnlyTokenAndNoFreeText_LeavesAnEmptyQuery()
    {
        IssueSearchParser.RemoveToken("env:prod", "env", "prod").Should().BeEmpty();
    }

    [Fact]
    public void RemoveToken_NullQuery_LeavesAnEmptyQuery()
    {
        IssueSearchParser.RemoveToken(null, "env", "prod").Should().BeEmpty();
    }

    [Fact]
    public void RemoveToken_NoTokensAtAll_KeepsTheFreeText()
    {
        IssueSearchParser.RemoveToken("crash timeout", "env", "prod").Should().Be("crash timeout");
    }

    [Fact]
    public void RemoveToken_ValueThatIsAPrefixOfAnotherValue_LeavesTheOtherOne()
    {
        var result = IssueSearchParser.RemoveToken("env:prod env:production", "env", "prod");

        result.Should().Be("env:production");
    }

    [Fact]
    public void RemoveToken_KeyThatIsAPrefixOfAnotherKey_LeavesTheOtherOne()
    {
        var result = IssueSearchParser.RemoveToken("env:prod env2:prod", "env", "prod");

        result.Should().Be("env2:prod");
    }

    [Fact]
    public void RemoveToken_SameKeyDifferentValue_LeavesTheOtherValue()
    {
        var result = IssueSearchParser.RemoveToken("env:prod env:staging", "env", "prod");

        result.Should().Be("env:staging");
    }

    [Fact]
    public void RemoveToken_QuotedValueWithSpaces_IsRemovedWhole()
    {
        var result = IssueSearchParser.RemoveToken("release:\"my app 1.0\" env:prod", "release", "my app 1.0");

        result.Should().Be("env:prod");
    }

    [Fact]
    public void RemoveToken_QuotedRemainingValue_StaysQuoted()
    {
        var result = IssueSearchParser.RemoveToken("release:\"my app 1.0\" env:prod", "env", "prod");

        result.Should().Be("release:\"my app 1.0\"");
    }

    [Fact]
    public void RemoveToken_ValueRepeatedInTheFreeText_DoesNotTouchTheFreeText()
    {
        var result = IssueSearchParser.RemoveToken("env:prod prod", "env", "prod");

        result.Should().Be("prod");
    }

    [Fact]
    public void RemoveToken_TokenRepeatedInTheQuery_RemovesEveryOccurrence()
    {
        var result = IssueSearchParser.RemoveToken("env:prod crash env:prod", "env", "prod");

        result.Should().Be("crash");
    }

    [Fact]
    public void RemoveToken_ReservedToken_LeavesTheOtherTokens()
    {
        var result = IssueSearchParser.RemoveToken("env:prod is:unassigned release:1.0", "is", "unassigned");

        result.Should().Be("env:prod release:1.0");
    }

    [Fact]
    public void RemoveToken_UnknownToken_KeepsEveryPart()
    {
        var result = IssueSearchParser.RemoveToken("crash env:prod timeout", "region", "eu");

        var (freeText, tags) = IssueSearchParser.Parse(result);
        freeText.Should().Be("crash timeout");
        tags.Should().ContainSingle().Which.Should().Be(("env", "prod"));
    }

    [Fact]
    public void RemoveToken_ResultIsAlwaysAQueryThatParsesBackToTheSameParts()
    {
        const string original = "crash env:prod timeout release:1.0 region:eu is:unviewed";
        const int tokenCount = 4;

        foreach (var (key, value) in IssueSearchParser.Parse(original).TagFilters)
        {
            var result = IssueSearchParser.RemoveToken(original, key, value);
            var (freeText, tags) = IssueSearchParser.Parse(result);

            tags.Should().NotContain(token => token.Key == key && token.Value == value);
            freeText.Should().Be("crash timeout");
            tags.Should().HaveCount(tokenCount - 1);
            IssueSearchParser.Serialize(freeText, tags).Should().Be(result);
        }
    }

    [Fact]
    public void RemoveToken_KeysAreCaseSensitive_AsParsed()
    {
        IssueSearchParser.RemoveToken("Env:prod", "env", "prod").Should().Be("Env:prod");
    }
}
