using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Projects;
using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Tests.Persistence;

public class ProjectLimitsServiceTests
{
    private static ProjectLimitsService Create(
        int? ageOverride = null,
        int? countOverride = null,
        int? rateLimitOverride = null,
        int defaultMaxAgeDays = 90,
        int defaultMaxEvents = 250_000,
        int checkIntervalMinutes = 60,
        int failedEnvelopeDays = 7,
        int rateLimitDefault = 50_000)
    {
        var projectConfig = Substitute.For<IProjectConfigService>();
        projectConfig.RetentionMaxAgeDays(Arg.Any<int>()).Returns(_ => ageOverride);
        projectConfig.RetentionMaxEventCount(Arg.Any<int>()).Returns(_ => countOverride);
        projectConfig.RateLimitMaxEventsPerWindow(Arg.Any<int>()).Returns(_ => rateLimitOverride);

        var appConfig = Substitute.For<IAppConfigService>();
        appConfig.RetentionDefaultMaxAgeDays.Returns(defaultMaxAgeDays);
        appConfig.RetentionDefaultMaxEvents.Returns(defaultMaxEvents);
        appConfig.RetentionCheckIntervalMinutes.Returns(checkIntervalMinutes);
        appConfig.RetentionFailedEnvelopeMaxAgeDays.Returns(failedEnvelopeDays);
        appConfig.RateLimitDefaultProjectLimit.Returns(rateLimitDefault);

        return new ProjectLimitsService(projectConfig, appConfig);
    }

    [Fact]
    public async Task GetAsync_WithoutOverrides_ReportsTheGlobalDefaults()
    {
        var service = Create();

        var limits = await service.GetAsync(7);

        limits.RetentionMaxAgeDaysOverride.Should().BeNull();
        limits.RetentionMaxAgeDays.Should().Be(90);
        limits.RetentionMaxEventCountOverride.Should().BeNull();
        limits.RetentionMaxEvents.Should().Be(250_000);
    }

    [Fact]
    public async Task GetAsync_WithOverrides_ReportsTheOverrideAndTheEffectiveValue()
    {
        var service = Create(ageOverride: 30, countOverride: 10_000);

        var limits = await service.GetAsync(7);

        limits.RetentionMaxAgeDaysOverride.Should().Be(30);
        limits.RetentionMaxAgeDays.Should().Be(30);
        limits.RetentionMaxEventCountOverride.Should().Be(10_000);
        limits.RetentionMaxEvents.Should().Be(10_000);
    }

    [Fact]
    public async Task GetAsync_ReportsTheGlobalDefaultsTheProjectInherits()
    {
        var service = Create(defaultMaxAgeDays: 45, defaultMaxEvents: 123_456);

        var limits = await service.GetAsync(7);

        limits.RetentionDefaultMaxAgeDays.Should().Be(45);
        limits.RetentionDefaultMaxEvents.Should().Be(123_456);
        limits.RetentionCheckIntervalMinutes.Should().Be(60);
        limits.RetentionFailedEnvelopeMaxAgeDays.Should().Be(7);
    }

    [Fact]
    public async Task GetAsync_ReportsTheRateLimit()
    {
        var service = Create(rateLimitOverride: 999);

        var limits = await service.GetAsync(7);

        limits.RateLimitMaxEventsPerWindowOverride.Should().Be(999);
        limits.RateLimitDefaultProjectLimit.Should().Be(50_000);
    }

    [Fact]
    public void AppConfig_DefaultEventCapIsTwoHundredAndFiftyThousand()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var appConfig = new AppConfigService(provider);

        appConfig.RetentionDefaultMaxEvents.Should().Be(250_000);
        appConfig.RetentionDefaultMaxAgeDays.Should().Be(90);
    }
}
