using Sigil.Application.Interfaces;
using Sigil.Application.Services;

namespace Sigil.Application.Tests.Services;

public class CompositeTagValueFormatterTests
{
    [Fact]
    public void Format_NoFormatters_ReturnsUnchanged()
    {
        var composite = new CompositeTagValueFormatter([]);

        composite.Format("key", "value").Should().Be("value");
    }

    [Fact]
    public void Format_SingleFormatter_Applied()
    {
        var inner = Substitute.For<IInternalTagValueFormatter>();
        inner.Format("release", "app@v1").Returns("v1");

        var composite = new CompositeTagValueFormatter([inner]);

        composite.Format("release", "app@v1").Should().Be("v1");
    }

    [Fact]
    public void Format_MultipleFormatters_ChainedInOrder()
    {
        var first = Substitute.For<IInternalTagValueFormatter>();
        first.Format("key", "abc").Returns("ABC");
        var second = Substitute.For<IInternalTagValueFormatter>();
        second.Format("key", "ABC").Returns("ABC!");

        var composite = new CompositeTagValueFormatter([first, second]);

        composite.Format("key", "abc").Should().Be("ABC!");
    }
}
