using Sigil.Application.Models.Projects;

namespace Sigil.Application.Tests.Models;

public class ProjectLimitsNoticeTests
{
    private static ProjectLimitsResponse Limits(
        int? ageOverride = null,
        int age = 90,
        int? countOverride = null,
        int count = 250_000) =>
        new(ageOverride, age, countOverride, count, 90, 250_000, 60, 7, null, 50_000);

    [Fact]
    public void Describe_WithoutOverrides_SaysTheProjectInheritsTheDefault()
    {
        var lines = ProjectLimitsNotice.Describe(Limits());

        lines.Should().HaveCount(3);
        lines[0].Should().Contain("90 days").And.Contain("inherits the global default");
    }

    [Fact]
    public void Describe_WithOverrides_SaysTheProjectOverridesTheDefault()
    {
        var lines = ProjectLimitsNotice.Describe(Limits(ageOverride: 30, age: 30));

        lines[0].Should().Contain("30 days").And.Contain("overrides the 90-day global default");
    }

    [Fact]
    public void Describe_AlwaysStatesTheEventCap()
    {
        var lines = ProjectLimitsNotice.Describe(Limits());

        lines[1].Should().Contain("250,000").And.Contain("oldest are deleted first");
    }

    [Fact]
    public void Describe_StatesTheSweepIntervalAndFailedEnvelopeWindow()
    {
        var lines = ProjectLimitsNotice.Describe(Limits());

        lines[2].Should().Contain("60 minutes").And.Contain("7 days");
    }

    [Fact]
    public void Describe_NullLimits_Throws()
    {
        var act = () => ProjectLimitsNotice.Describe(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
