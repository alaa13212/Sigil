namespace Sigil.Domain.Tests;

public class SystemTagsTests
{
    [Theory]
    [InlineData("sigil.regression")]
    [InlineData("sigil.reopened")]
    [InlineData("sigil.high-volume")]
    public void IsSystemTag_SystemTag_ReturnsTrue(string tag)
    {
        SystemTags.IsSystemTag(tag).Should().BeTrue();
    }

    [Theory]
    [InlineData("custom-tag")]
    [InlineData("")]
    [InlineData("environment")]
    public void IsSystemTag_NonSystemTag_ReturnsFalse(string tag)
    {
        SystemTags.IsSystemTag(tag).Should().BeFalse();
    }

    [Fact]
    public void IsSystemTag_CaseSensitive_UppercaseReturnsFalse()
    {
        SystemTags.IsSystemTag("SIGIL.regression").Should().BeFalse();
        SystemTags.IsSystemTag("Sigil.regression").Should().BeFalse();
    }

    [Fact]
    public void All_ContainsExpectedTags()
    {
        SystemTags.All.Should().HaveCount(3);
        SystemTags.All.Should().Contain(SystemTags.Regression);
        SystemTags.All.Should().Contain(SystemTags.Reopened);
        SystemTags.All.Should().Contain(SystemTags.HighVolume);
    }

    [Fact]
    public void AllPairs_HasMatchingCount()
    {
        SystemTags.AllPairs.Should().HaveCount(SystemTags.All.Length);
    }

    [Fact]
    public void AllPairs_AllValuesAreTrue()
    {
        SystemTags.AllPairs.Should().AllSatisfy(p => p.Value.Should().Be("true"));
    }
}
