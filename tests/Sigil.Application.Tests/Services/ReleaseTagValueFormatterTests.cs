using Sigil.Application.Services;

namespace Sigil.Application.Tests.Services;

public class ReleaseTagValueFormatterTests
{
    private readonly ReleaseTagValueFormatter _formatter = new();

    [Fact]
    public void Format_ReleaseKeyWithAt_StripsPrefix()
    {
        _formatter.Format("release", "myapp@v1.0.0").Should().Be("v1.0.0");
    }

    [Fact]
    public void Format_ReleaseKeyNoAt_ReturnsUnchanged()
    {
        _formatter.Format("release", "v1.0.0").Should().Be("v1.0.0");
    }

    [Fact]
    public void Format_NonReleaseKey_ReturnsUnchanged()
    {
        _formatter.Format("environment", "myapp@v1.0.0").Should().Be("myapp@v1.0.0");
    }
}
