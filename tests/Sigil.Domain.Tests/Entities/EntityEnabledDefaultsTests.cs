using Sigil.Domain.Entities;

namespace Sigil.Domain.Tests.Entities;

public class EntityEnabledDefaultsTests
{
    [Fact]
    public void AlertRule_EnabledDefaultsToTrue()
    {
        new AlertRule { Name = "test" }.Enabled.Should().BeTrue();
    }

    [Fact]
    public void AutoTagRule_EnabledDefaultsToTrue()
    {
        new AutoTagRule
        {
            Field = "message", Value = "error", TagKey = "type", TagValue = "bug",
        }.Enabled.Should().BeTrue();
    }

    [Fact]
    public void EventFilter_EnabledDefaultsToTrue()
    {
        new EventFilter { Field = "message", Value = "test" }.Enabled.Should().BeTrue();
    }

    [Fact]
    public void IssueTrackerConfig_EnabledDefaultsToTrue()
    {
        new IssueTrackerConfig { EncryptedConfig = "{}" }.Enabled.Should().BeTrue();
    }

    [Fact]
    public void StackTraceFilter_EnabledDefaultsToTrue()
    {
        new StackTraceFilter { Field = "function", Value = "test" }.Enabled.Should().BeTrue();
    }

    [Fact]
    public void TextNormalizationRule_EnabledDefaultsToTrue()
    {
        new TextNormalizationRule { Pattern = ".*", Replacement = "" }.Enabled.Should().BeTrue();
    }
}
