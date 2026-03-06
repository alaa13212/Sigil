using Sigil.Domain.Extensions;

namespace Sigil.Domain.Tests.Extensions;

public class EnumerableExtensionTests
{
    [Fact]
    public void IsNullOrEmpty_Null_ReturnsTrue()
    {
        ((IEnumerable<int>?)null).IsNullOrEmpty().Should().BeTrue();
    }

    [Fact]
    public void IsNullOrEmpty_EmptyList_ReturnsTrue()
    {
        new List<int>().IsNullOrEmpty().Should().BeTrue();
    }

    [Fact]
    public void IsNullOrEmpty_NonEmptyList_ReturnsFalse()
    {
        new List<int> { 1 }.IsNullOrEmpty().Should().BeFalse();
    }

    [Fact]
    public void IsEmpty_EmptyList_ReturnsTrue()
    {
        new List<string>().IsEmpty().Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_NonEmptyList_ReturnsFalse()
    {
        new List<string> { "a" }.IsEmpty().Should().BeFalse();
    }
}
