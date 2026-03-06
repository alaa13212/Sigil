using Sigil.Domain.Extensions;

namespace Sigil.Domain.Tests.Extensions;

public class TimeMathTests
{
    private static readonly DateTime Earlier = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = new(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Earlier_ABeforeB_ReturnsA()
    {
        TimeMath.Earlier(Earlier, Later).Should().Be(Earlier);
    }

    [Fact]
    public void Earlier_AAfterB_ReturnsB()
    {
        TimeMath.Earlier(Later, Earlier).Should().Be(Earlier);
    }

    [Fact]
    public void Earlier_Equal_ReturnsEither()
    {
        TimeMath.Earlier(Earlier, Earlier).Should().Be(Earlier);
    }

    [Fact]
    public void Later_ABeforeB_ReturnsB()
    {
        TimeMath.Later(Earlier, Later).Should().Be(Later);
    }

    [Fact]
    public void Later_AAfterB_ReturnsA()
    {
        TimeMath.Later(Later, Earlier).Should().Be(Later);
    }

    [Fact]
    public void Later_Equal_ReturnsEither()
    {
        TimeMath.Later(Later, Later).Should().Be(Later);
    }
}
