using Sigil.Application.Models.Issues;

namespace Sigil.Application.Tests.Models;

public class IssueSearchCriteriaTests
{
    [Fact]
    public void ParseCriteria_EmptyQuery_HasNothingToFilter()
    {
        var criteria = IssueSearchParser.ParseCriteria(null);

        criteria.FreeText.Should().BeNull();
        criteria.TagFilters.Should().BeEmpty();
        criteria.Releases.Should().BeEmpty();
        criteria.Assignment.Should().Be(IssueAssignmentFilter.Any);
        criteria.Bookmarked.Should().BeFalse();
        criteria.Unviewed.Should().BeFalse();
    }

    [Fact]
    public void ParseCriteria_FreeTextOnly_LeavesNothingReserved()
    {
        var criteria = IssueSearchParser.ParseCriteria("database timeout");

        criteria.FreeText.Should().Be("database timeout");
        criteria.TagFilters.Should().BeEmpty();
        criteria.Releases.Should().BeEmpty();
    }

    [Fact]
    public void ParseCriteria_ReleaseToken_BecomesARelease()
    {
        var criteria = IssueSearchParser.ParseCriteria("release:1.2.3");

        criteria.Releases.Should().ContainSingle().Which.Should().Be("1.2.3");
        criteria.TagFilters.Should().BeEmpty();
    }

    [Fact]
    public void ParseCriteria_QuotedReleaseToken_KeepsSpacesInTheName()
    {
        var criteria = IssueSearchParser.ParseCriteria("release:\"my app 1.0\"");

        criteria.Releases.Should().ContainSingle().Which.Should().Be("my app 1.0");
    }

    [Fact]
    public void ParseCriteria_SeveralReleaseTokens_AreAllKept()
    {
        var criteria = IssueSearchParser.ParseCriteria("release:1.0 release:2.0");

        criteria.Releases.Should().BeEquivalentTo(["1.0", "2.0"], options => options.WithStrictOrdering());
    }

    [Fact]
    public void ParseCriteria_ReleaseToken_IsNotLeftAsATagFilter()
    {
        var criteria = IssueSearchParser.ParseCriteria("env:prod release:1.0 crash");

        criteria.TagFilters.Should().ContainSingle().Which.Key.Should().Be("env");
        criteria.Releases.Should().ContainSingle().Which.Should().Be("1.0");
        criteria.FreeText.Should().Be("crash");
    }

    [Fact]
    public void ParseCriteria_ReservedKeys_AreCaseInsensitive()
    {
        var criteria = IssueSearchParser.ParseCriteria("Release:1.0 IS:assigned");

        criteria.Releases.Should().ContainSingle().Which.Should().Be("1.0");
        criteria.Assignment.Should().Be(IssueAssignmentFilter.Assigned);
    }

    [Theory]
    [InlineData("is:assigned", IssueAssignmentFilter.Assigned)]
    [InlineData("is:unassigned", IssueAssignmentFilter.Unassigned)]
    public void ParseCriteria_AssignmentTokens_SelectAssignmentState(string search, IssueAssignmentFilter expected)
    {
        IssueSearchParser.ParseCriteria(search).Assignment.Should().Be(expected);
    }

    [Fact]
    public void ParseCriteria_BookmarkedToken_SetsBookmarked()
    {
        var criteria = IssueSearchParser.ParseCriteria("is:bookmarked");

        criteria.Bookmarked.Should().BeTrue();
        criteria.TagFilters.Should().BeEmpty();
    }

    [Fact]
    public void ParseCriteria_UnviewedToken_SetsUnviewed()
    {
        var criteria = IssueSearchParser.ParseCriteria("is:unviewed");

        criteria.Unviewed.Should().BeTrue();
        criteria.TagFilters.Should().BeEmpty();
    }

    [Fact]
    public void ParseCriteria_UnknownIsValue_StaysAnOrdinaryTagFilter()
    {
        var criteria = IssueSearchParser.ParseCriteria("is:regression");

        criteria.TagFilters.Should().ContainSingle().Which.Should().Be(("is", "regression"));
        criteria.Assignment.Should().Be(IssueAssignmentFilter.Any);
    }

    [Fact]
    public void ParseCriteria_RepeatedAssignmentToken_LastOneWins()
    {
        IssueSearchParser.ParseCriteria("is:assigned is:unassigned").Assignment
            .Should().Be(IssueAssignmentFilter.Unassigned);
    }

    [Fact]
    public void ParseCriteria_MixedTokensAndFreeText_SplitsEveryPart()
    {
        var criteria = IssueSearchParser.ParseCriteria("crash env:prod release:1.0 is:unviewed timeout");

        criteria.FreeText.Should().Be("crash timeout");
        criteria.TagFilters.Should().ContainSingle().Which.Should().Be(("env", "prod"));
        criteria.Releases.Should().ContainSingle().Which.Should().Be("1.0");
        criteria.Unviewed.Should().BeTrue();
    }

    [Fact]
    public void ParseCriteria_SeveralTagFilters_KeepsAllOfThem()
    {
        var criteria = IssueSearchParser.ParseCriteria("env:prod env:staging region:eu");

        criteria.TagFilters.Should().BeEquivalentTo(
        [
            ("env", "prod"),
            ("env", "staging"),
            ("region", "eu")
        ]);
    }

    [Fact]
    public void ParseCriteria_TagFilterIsNotDisturbedByReservedTokens()
    {
        var criteria = IssueSearchParser.ParseCriteria("env:prod is:assigned is:bookmarked");

        criteria.TagFilters.Should().ContainSingle().Which.Should().Be(("env", "prod"));
        criteria.Assignment.Should().Be(IssueAssignmentFilter.Assigned);
        criteria.Bookmarked.Should().BeTrue();
    }
}
