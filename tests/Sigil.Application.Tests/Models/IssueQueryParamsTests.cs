using Sigil.Application.Models;

namespace Sigil.Application.Tests.Models;

public class IssueQueryParamsTests
{
    [Fact]
    public void Default_SortDescendingIsTrue()
    {
        // SortDescending defaults to true — changing it silently reverses API sort order
        new IssueQueryParams().SortDescending.Should().BeTrue();
    }
}
