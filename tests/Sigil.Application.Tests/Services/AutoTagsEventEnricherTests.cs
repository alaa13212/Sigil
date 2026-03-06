using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Services;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Tests.Services;

public class AutoTagsEventEnricherTests
{
    private readonly IRuleEngine _ruleEngine;
    private readonly AutoTagsEventEnricher _enricher;

    public AutoTagsEventEnricherTests()
    {
        _ruleEngine = Substitute.For<IRuleEngine>();
        _enricher = new AutoTagsEventEnricher(_ruleEngine);
    }

    private static ParsedEvent MakeEvent() => new()
    {
        EventId = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = Severity.Error,
        RawJson = "{}",
    };

    private static EventParsingContext MakeContext(params AutoTagRule[] rules) => new()
    {
        ProjectId = 1,
        NormalizationRules = [],
        AutoTagRules = [..rules],
        InboundFilters = [],
        StackTraceFilters = [],
    };

    [Fact]
    public void Enrich_DisabledRule_Skipped()
    {
        var rule = new AutoTagRule
        {
            Field = "message", Operator = FilterOperator.Equals, Value = "test",
            TagKey = "tag", TagValue = "val", Enabled = false,
        };
        var evt = MakeEvent();

        _enricher.Enrich(evt, MakeContext(rule));

        _ruleEngine.DidNotReceive().Evaluate(Arg.Any<RuleCondition>(), Arg.Any<ParsedEvent>());
    }

    [Fact]
    public void Enrich_EnabledRuleMatching_TagAdded()
    {
        var rule = new AutoTagRule
        {
            Field = "message", Operator = FilterOperator.Equals, Value = "test",
            TagKey = "browser", TagValue = "Chrome", Enabled = true,
        };
        _ruleEngine.Evaluate(Arg.Any<RuleCondition>(), Arg.Any<ParsedEvent>()).Returns(true);
        var evt = MakeEvent();

        _enricher.Enrich(evt, MakeContext(rule));

        evt.Tags!["browser"].Should().Be("Chrome");
    }

    [Fact]
    public void Enrich_TagsNull_InitializesAndAdds()
    {
        var rule = new AutoTagRule
        {
            Field = "message", Operator = FilterOperator.Equals, Value = "test",
            TagKey = "key", TagValue = "val", Enabled = true,
        };
        _ruleEngine.Evaluate(Arg.Any<RuleCondition>(), Arg.Any<ParsedEvent>()).Returns(true);
        var evt = MakeEvent();
        evt.Tags = null;

        _enricher.Enrich(evt, MakeContext(rule));

        evt.Tags.Should().NotBeNull();
        evt.Tags!["key"].Should().Be("val");
    }

    [Fact]
    public void Enrich_MultipleMatchingRules_AllTagsAdded()
    {
        var rule1 = new AutoTagRule
        {
            Field = "message", Operator = FilterOperator.Equals, Value = "test",
            TagKey = "tag1", TagValue = "val1", Enabled = true,
        };
        var rule2 = new AutoTagRule
        {
            Field = "environment", Operator = FilterOperator.Equals, Value = "prod",
            TagKey = "tag2", TagValue = "val2", Enabled = true,
        };
        _ruleEngine.Evaluate(Arg.Any<RuleCondition>(), Arg.Any<ParsedEvent>()).Returns(true);
        var evt = MakeEvent();

        _enricher.Enrich(evt, MakeContext(rule1, rule2));

        evt.Tags!["tag1"].Should().Be("val1");
        evt.Tags!["tag2"].Should().Be("val2");
    }
}
