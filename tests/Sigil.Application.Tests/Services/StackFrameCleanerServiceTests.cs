using Sigil.Application.Interfaces;
using Sigil.Application.Services;
using Sigil.Domain.Enums;

namespace Sigil.Application.Tests.Services;

public class StackFrameCleanerServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CleanMethodName_NullOrEmpty_ReturnsUnknown(string? methodName)
    {
        var service = new StackFrameCleanerService([]);

        service.CleanMethodName(Platform.CSharp, methodName).Should().Be("(unknown)");
    }

    [Fact]
    public void CleanMethodName_KnownPlatform_DispatchesToCleaner()
    {
        var mockCleaner = Substitute.For<IStackFrameCleaner>();
        mockCleaner.Platform.Returns(Platform.CSharp);
        mockCleaner.CleanMethodName("Test").Returns("Cleaned");

        var service = new StackFrameCleanerService([mockCleaner]);

        service.CleanMethodName(Platform.CSharp, "Test").Should().Be("Cleaned");
    }

    [Fact]
    public void CleanMethodName_UnknownPlatform_ReturnsRawName()
    {
        var service = new StackFrameCleanerService([]);

        service.CleanMethodName(Platform.Go, "myFunc").Should().Be("myFunc");
    }
}
