using Sigil.Application.Interfaces;
using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class SlidingWindowRateLimiterTests
{
    private readonly IAppConfigService _appConfig;
    private readonly SlidingWindowRateLimiter _limiter;

    public SlidingWindowRateLimiterTests()
    {
        _appConfig = Substitute.For<IAppConfigService>();
        _appConfig.RateLimitWindowSeconds.Returns(60);
        _appConfig.RateLimitGlobalLimit.Returns(1000);
        _appConfig.RateLimitDefaultProjectLimit.Returns(100);
        _limiter = new SlidingWindowRateLimiter(_appConfig);
    }

    [Fact]
    public void TryAcquire_UnderLimit_ReturnsTrue()
    {
        _limiter.TryAcquire(projectId: 1).Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_AtProjectLimit_ReturnsFalse()
    {
        _appConfig.RateLimitDefaultProjectLimit.Returns(3);

        for (int i = 0; i < 3; i++)
            _limiter.TryAcquire(projectId: 1).Should().BeTrue();

        _limiter.TryAcquire(projectId: 1).Should().BeFalse();
    }

    [Fact]
    public void TryAcquire_DifferentProjects_IndependentCounters()
    {
        _appConfig.RateLimitDefaultProjectLimit.Returns(2);

        _limiter.TryAcquire(projectId: 1).Should().BeTrue();
        _limiter.TryAcquire(projectId: 1).Should().BeTrue();
        _limiter.TryAcquire(projectId: 1).Should().BeFalse();

        // Project 2 has its own counter
        _limiter.TryAcquire(projectId: 2).Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_CustomProjectLimit_OverridesDefault()
    {
        _appConfig.RateLimitDefaultProjectLimit.Returns(100);

        _limiter.TryAcquire(projectId: 1, projectLimit: 1).Should().BeTrue();
        _limiter.TryAcquire(projectId: 1, projectLimit: 1).Should().BeFalse();
    }

    [Fact]
    public void TryAcquire_GlobalLimitReached_ReturnsFalse()
    {
        _appConfig.RateLimitGlobalLimit.Returns(2);
        _appConfig.RateLimitDefaultProjectLimit.Returns(100);

        _limiter.TryAcquire(projectId: 1).Should().BeTrue();
        _limiter.TryAcquire(projectId: 2).Should().BeTrue();
        _limiter.TryAcquire(projectId: 3).Should().BeFalse();
    }

    [Fact]
    public async Task TryAcquire_AfterWindowExpires_ResetsCounter()
    {
        // Use a fresh limiter with a 1-second window
        var config = Substitute.For<IAppConfigService>();
        config.RateLimitWindowSeconds.Returns(1);
        config.RateLimitGlobalLimit.Returns(1000);
        config.RateLimitDefaultProjectLimit.Returns(1);
        var limiter = new SlidingWindowRateLimiter(config);

        limiter.TryAcquire(projectId: 99).Should().BeTrue();
        limiter.TryAcquire(projectId: 99).Should().BeFalse();

        await Task.Delay(1100); // wait for window to expire

        limiter.TryAcquire(projectId: 99).Should().BeTrue();
    }
}
