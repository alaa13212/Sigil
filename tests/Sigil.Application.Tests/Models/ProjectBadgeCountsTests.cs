using Sigil.Application.Models;

namespace Sigil.Application.Tests.Models;

public class ProjectBadgeCountsTests
{
    [Fact]
    public void Deconstruct_ReturnsUnseenIssuesAndReleases()
    {
        var counts = new ProjectBadgeCounts(5, 3);
        var (issues, releases) = counts;
        issues.Should().Be(5);
        releases.Should().Be(3);
    }

    [Fact]
    public void Empty_HasZeroCounts()
    {
        var (issues, releases) = ProjectBadgeCounts.Empty;
        issues.Should().Be(0);
        releases.Should().Be(0);
    }
}
