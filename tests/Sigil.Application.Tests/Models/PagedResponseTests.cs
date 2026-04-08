using Sigil.Application.Models;

namespace Sigil.Application.Tests.Models;

public class PagedResponseTests
{
    [Theory]
    [InlineData(10, 3, 4)]  // ceil(10/3) = 4
    [InlineData(10, 5, 2)]  // ceil(10/5) = 2
    [InlineData(10, 10, 1)] // exact fit
    [InlineData(11, 5, 3)]  // ceil(11/5) = 3
    [InlineData(0, 5, 0)]   // empty result set
    public void TotalPages_EqualsRoundedUpQuotient(int totalCount, int pageSize, int expected)
    {
        var r = new PagedResponse<int>([], totalCount, 1, pageSize);
        r.TotalPages.Should().Be(expected);
    }

    [Fact]
    public void HasNext_TrueWhenCurrentPageIsBeforeLastPage()
    {
        var r = new PagedResponse<int>([], 10, 1, 5); // TotalPages = 2, Page = 1
        r.HasNext.Should().BeTrue();
    }

    [Fact]
    public void HasNext_FalseWhenOnLastPage()
    {
        var r = new PagedResponse<int>([], 10, 2, 5); // TotalPages = 2, Page = 2
        r.HasNext.Should().BeFalse();
    }

    [Fact]
    public void HasPrevious_TrueWhenNotOnFirstPage()
    {
        var r = new PagedResponse<int>([], 10, 2, 5);
        r.HasPrevious.Should().BeTrue();
    }

    [Fact]
    public void HasPrevious_FalseWhenOnFirstPage()
    {
        var r = new PagedResponse<int>([], 10, 1, 5);
        r.HasPrevious.Should().BeFalse();
    }

    [Fact]
    public void HasNext_FalseWhenEmpty()
    {
        var r = new PagedResponse<int>([], 0, 1, 10); // TotalPages = 0
        r.HasNext.Should().BeFalse();
    }
}
