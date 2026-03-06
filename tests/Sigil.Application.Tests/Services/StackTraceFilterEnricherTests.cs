using Sigil.Application.Interfaces;
using Sigil.Application.Services;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Tests.Services;

public class StackTraceFilterEnricherTests
{
    private readonly IRuleEngine _ruleEngine;
    private readonly StackTraceFilterEnricher _enricher;

    public StackTraceFilterEnricherTests()
    {
        _ruleEngine = Substitute.For<IRuleEngine>();
        _enricher = new StackTraceFilterEnricher(_ruleEngine);
    }

    private static ParsedEvent MakeEvent(List<ParsedStackFrame>? frames = null) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = Severity.Error,
        RawJson = "{}",
        Stacktrace = frames ?? [],
    };

    private static EventParsingContext MakeContext(params StackTraceFilter[] filters) => new()
    {
        ProjectId = 1,
        NormalizationRules = [],
        AutoTagRules = [],
        InboundFilters = [],
        StackTraceFilters = [..filters],
    };

    [Fact]
    public void Enrich_EmptyFilters_NoChange()
    {
        var frames = new List<ParsedStackFrame>
        {
            new() { Function = "Main" },
        };
        var evt = MakeEvent(frames);

        _enricher.Enrich(evt, MakeContext());

        evt.Stacktrace.Should().HaveCount(1);
    }

    [Fact]
    public void Enrich_EmptyStacktrace_NoCrash()
    {
        var filter = new StackTraceFilter
        {
            Field = "function", Operator = FilterOperator.Contains, Value = "test", Enabled = true,
        };
        var evt = MakeEvent([]);

        var act = () => _enricher.Enrich(evt, MakeContext(filter));

        act.Should().NotThrow();
    }

    [Fact]
    public void Enrich_MatchingFunctionFilter_FrameRemoved()
    {
        var filter = new StackTraceFilter
        {
            Field = "function", Operator = FilterOperator.Contains, Value = "Internal", Enabled = true,
        };
        _ruleEngine.Match("InternalMethod", FilterOperator.Contains, "Internal").Returns(true);
        _ruleEngine.Match("PublicMethod", FilterOperator.Contains, "Internal").Returns(false);

        var evt = MakeEvent(
        [
            new ParsedStackFrame { Function = "InternalMethod" },
            new ParsedStackFrame { Function = "PublicMethod" },
        ]);

        _enricher.Enrich(evt, MakeContext(filter));

        evt.Stacktrace.Should().HaveCount(1);
        evt.Stacktrace[0].Function.Should().Be("PublicMethod");
    }

    [Fact]
    public void Enrich_MatchingFilenameFilter_FrameRemoved()
    {
        var filter = new StackTraceFilter
        {
            Field = "filename", Operator = FilterOperator.Contains, Value = "vendor", Enabled = true,
        };
        _ruleEngine.Match("vendor/lib.js", FilterOperator.Contains, "vendor").Returns(true);
        _ruleEngine.Match("src/app.js", FilterOperator.Contains, "vendor").Returns(false);

        var evt = MakeEvent(
        [
            new ParsedStackFrame { Filename = "vendor/lib.js", Function = "x" },
            new ParsedStackFrame { Filename = "src/app.js", Function = "y" },
        ]);

        _enricher.Enrich(evt, MakeContext(filter));

        evt.Stacktrace.Should().HaveCount(1);
        evt.Stacktrace[0].Filename.Should().Be("src/app.js");
    }

    [Fact]
    public void Enrich_MatchingModuleFilter_FrameRemoved()
    {
        var filter = new StackTraceFilter
        {
            Field = "module", Operator = FilterOperator.Equals, Value = "System.Core", Enabled = true,
        };
        _ruleEngine.Match("System.Core", FilterOperator.Equals, "System.Core").Returns(true);

        var evt = MakeEvent(
        [
            new ParsedStackFrame { Module = "System.Core", Function = "x" },
            new ParsedStackFrame { Module = "MyApp", Function = "y" },
        ]);

        _enricher.Enrich(evt, MakeContext(filter));

        evt.Stacktrace.Should().HaveCount(1);
        evt.Stacktrace[0].Module.Should().Be("MyApp");
    }

    [Fact]
    public void Enrich_DisabledFilter_FrameKept()
    {
        var filter = new StackTraceFilter
        {
            Field = "function", Operator = FilterOperator.Contains, Value = "Internal", Enabled = false,
        };
        var evt = MakeEvent([new ParsedStackFrame { Function = "InternalMethod" }]);

        _enricher.Enrich(evt, MakeContext(filter));

        evt.Stacktrace.Should().HaveCount(1);
    }

    [Fact]
    public void Enrich_UnknownField_FrameKept()
    {
        var filter = new StackTraceFilter
        {
            Field = "unknown", Operator = FilterOperator.Contains, Value = "test", Enabled = true,
        };
        var evt = MakeEvent([new ParsedStackFrame { Function = "MyMethod" }]);

        _enricher.Enrich(evt, MakeContext(filter));

        evt.Stacktrace.Should().HaveCount(1);
    }
}
