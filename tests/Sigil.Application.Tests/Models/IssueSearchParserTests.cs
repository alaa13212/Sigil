using Sigil.Application.Models.Issues;

namespace Sigil.Application.Tests.Models;

public class IssueSearchParserTests
{
    [Fact]
    public void Parse_Null_ReturnsEmpty()
    {
        var (freeText, tags) = IssueSearchParser.Parse(null);
        freeText.Should().BeNull();
        tags.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        var (freeText, tags) = IssueSearchParser.Parse("");
        freeText.Should().BeNull();
        tags.Should().BeEmpty();
    }

    [Fact]
    public void Parse_FreeTextOnly_ReturnsFreeText()
    {
        var (freeText, tags) = IssueSearchParser.Parse("hello world");
        freeText.Should().Be("hello world");
        tags.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleTagFilter_ReturnsTag()
    {
        var (freeText, tags) = IssueSearchParser.Parse("env:production");
        freeText.Should().BeNull();
        tags.Should().ContainSingle(t => t.Key == "env" && t.Value == "production");
    }

    [Fact]
    public void Parse_QuotedTagValue_ReturnsValueWithSpaces()
    {
        var (_, tags) = IssueSearchParser.Parse("release:\"my app 1.0\"");
        tags.Should().ContainSingle(t => t.Key == "release" && t.Value == "my app 1.0");
    }

    [Fact]
    public void Parse_MultipleTagFilters_ReturnsAll()
    {
        var (_, tags) = IssueSearchParser.Parse("env:prod release:1.0");
        tags.Should().HaveCount(2);
        tags.Should().Contain(t => t.Key == "env" && t.Value == "prod");
        tags.Should().Contain(t => t.Key == "release" && t.Value == "1.0");
    }

    [Fact]
    public void Parse_MixedFreeTextAndTags_ReturnsBoth()
    {
        var (freeText, tags) = IssueSearchParser.Parse("error env:prod crash");
        freeText.Should().Be("error crash");
        tags.Should().ContainSingle(t => t.Key == "env" && t.Value == "prod");
    }

    [Fact]
    public void Parse_QuotedValueWithSpaces_ParsedCorrectly()
    {
        var (_, tags) = IssueSearchParser.Parse("tag:\"value with spaces\"");
        tags.Should().ContainSingle(t => t.Key == "tag" && t.Value == "value with spaces");
    }

    [Fact]
    public void Serialize_SimpleTag_ProducesKeyColon()
    {
        var result = IssueSearchParser.Serialize(null, [("env", "prod")]);
        result.Should().Be("env:prod");
    }

    [Fact]
    public void Serialize_TagWithSpaces_QuotesValue()
    {
        var result = IssueSearchParser.Serialize(null, [("release", "my app 1.0")]);
        result.Should().Be("release:\"my app 1.0\"");
    }

    [Fact]
    public void Serialize_FreeTextAndTag_BothIncluded()
    {
        var result = IssueSearchParser.Serialize("crash", [("env", "prod")]);
        result.Should().Contain("env:prod");
        result.Should().Contain("crash");
    }

    [Fact]
    public void RoundTrip_SimpleTag_PreservesValue()
    {
        var original = "env:prod";
        var (freeText, tags) = IssueSearchParser.Parse(original);
        var serialized = IssueSearchParser.Serialize(freeText, tags);
        serialized.Should().Be(original);
    }

    [Fact]
    public void RoundTrip_QuotedTag_PreservesValue()
    {
        var original = "release:\"my app 1.0\"";
        var (freeText, tags) = IssueSearchParser.Parse(original);
        var serialized = IssueSearchParser.Serialize(freeText, tags);
        serialized.Should().Be(original);
    }
}
