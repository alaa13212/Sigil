using Sigil.Application.Models;
using Sigil.Application.Services;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Tests.Services;

public class RuleEngineTests
{
    private readonly RuleEngine _engine = new();

    private static ParsedEvent MakeEvent(
        string? exceptionType = null,
        string? message = null,
        string? normalizedMessage = null,
        string? release = null,
        string? environment = null,
        string? logger = null,
        Severity level = Severity.Error,
        string? culprit = null,
        string? serverName = null,
        string? fingerprint = null,
        List<ParsedStackFrame>? stacktrace = null,
        Dictionary<string, string>? tags = null) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = level,
        RawJson = "{}",
        ExceptionType = exceptionType,
        Message = message,
        NormalizedMessage = normalizedMessage,
        Release = release,
        Environment = environment,
        Logger = logger,
        Culprit = culprit,
        ServerName = serverName,
        Fingerprint = fingerprint,
        Stacktrace = stacktrace ?? [],
        Tags = tags,
    };

    // --- Match operator tests ---

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("Hello", "hello")]
    [InlineData("HELLO", "hello")]
    public void Match_Equals_CaseInsensitive(string value, string pattern)
    {
        _engine.Match(value, FilterOperator.Equals, pattern).Should().BeTrue();
    }

    [Fact]
    public void Match_Equals_Mismatch_ReturnsFalse()
    {
        _engine.Match("hello", FilterOperator.Equals, "world").Should().BeFalse();
    }

    [Theory]
    [InlineData("hello world", "world")]
    [InlineData("Hello World", "hello")]
    public void Match_Contains_CaseInsensitive(string value, string pattern)
    {
        _engine.Match(value, FilterOperator.Contains, pattern).Should().BeTrue();
    }

    [Fact]
    public void Match_Contains_Mismatch_ReturnsFalse()
    {
        _engine.Match("hello", FilterOperator.Contains, "xyz").Should().BeFalse();
    }

    [Theory]
    [InlineData("hello world", "hello")]
    [InlineData("Hello World", "hello")]
    public void Match_StartsWith_CaseInsensitive(string value, string pattern)
    {
        _engine.Match(value, FilterOperator.StartsWith, pattern).Should().BeTrue();
    }

    [Fact]
    public void Match_StartsWith_Mismatch_ReturnsFalse()
    {
        _engine.Match("hello", FilterOperator.StartsWith, "world").Should().BeFalse();
    }

    [Theory]
    [InlineData("hello world", "world")]
    [InlineData("Hello World", "WORLD")]
    public void Match_EndsWith_CaseInsensitive(string value, string pattern)
    {
        _engine.Match(value, FilterOperator.EndsWith, pattern).Should().BeTrue();
    }

    [Fact]
    public void Match_EndsWith_Mismatch_ReturnsFalse()
    {
        _engine.Match("hello", FilterOperator.EndsWith, "xyz").Should().BeFalse();
    }

    [Fact]
    public void Match_Regex_ValidPattern_ReturnsTrue()
    {
        _engine.Match("error-42", FilterOperator.Regex, @"error-\d+").Should().BeTrue();
    }

    [Fact]
    public void Match_Regex_NoMatch_ReturnsFalse()
    {
        _engine.Match("hello", FilterOperator.Regex, @"^\d+$").Should().BeFalse();
    }

    [Fact]
    public void Match_Regex_InvalidPattern_ReturnsFalse()
    {
        _engine.Match("hello", FilterOperator.Regex, "[invalid").Should().BeFalse();
    }

    // --- Evaluate field resolution tests ---

    [Fact]
    public void Evaluate_ExceptionType_Resolves()
    {
        var evt = MakeEvent(exceptionType: "NullReferenceException");
        var cond = new RuleCondition("exceptionType", FilterOperator.Equals, "NullReferenceException");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Message_Resolves()
    {
        var evt = MakeEvent(message: "Something failed");
        var cond = new RuleCondition("message", FilterOperator.Contains, "failed");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_NormalizedMessage_Resolves()
    {
        var evt = MakeEvent(normalizedMessage: "normalized msg");
        var cond = new RuleCondition("normalizedMessage", FilterOperator.Equals, "normalized msg");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Release_Resolves()
    {
        var evt = MakeEvent(release: "v1.0.0");
        var cond = new RuleCondition("release", FilterOperator.Equals, "v1.0.0");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Environment_Resolves()
    {
        var evt = MakeEvent(environment: "production");
        var cond = new RuleCondition("environment", FilterOperator.Equals, "production");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Logger_Resolves()
    {
        var evt = MakeEvent(logger: "MyLogger");
        var cond = new RuleCondition("logger", FilterOperator.Equals, "MyLogger");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Level_Resolves()
    {
        var evt = MakeEvent(level: Severity.Warning);
        var cond = new RuleCondition("level", FilterOperator.Equals, "Warning");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Culprit_Resolves()
    {
        var evt = MakeEvent(culprit: "MyClass.MyMethod");
        var cond = new RuleCondition("culprit", FilterOperator.Contains, "MyClass");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ServerName_Resolves()
    {
        var evt = MakeEvent(serverName: "web-01");
        var cond = new RuleCondition("serverName", FilterOperator.Equals, "web-01");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Fingerprint_Resolves()
    {
        var evt = MakeEvent(fingerprint: "abc123");
        var cond = new RuleCondition("fingerprint", FilterOperator.Equals, "abc123");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Stacktrace_JoinsFunctions()
    {
        var evt = MakeEvent(stacktrace:
        [
            new ParsedStackFrame { Function = "Main" },
            new ParsedStackFrame { Function = "Run" },
        ]);
        var cond = new RuleCondition("stacktrace", FilterOperator.Contains, "Main");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_Stacktrace_Empty_ReturnsFalse()
    {
        var evt = MakeEvent(stacktrace: []);
        var cond = new RuleCondition("stacktrace", FilterOperator.Contains, "anything");

        _engine.Evaluate(cond, evt).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_TagField_Resolves()
    {
        var evt = MakeEvent(tags: new Dictionary<string, string> { ["browser"] = "Chrome" });
        var cond = new RuleCondition("tag:browser", FilterOperator.Equals, "Chrome");

        _engine.Evaluate(cond, evt).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_TagField_MissingTag_ReturnsFalse()
    {
        var evt = MakeEvent(tags: new Dictionary<string, string>());
        var cond = new RuleCondition("tag:missing", FilterOperator.Equals, "value");

        _engine.Evaluate(cond, evt).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_UnknownField_ReturnsFalse()
    {
        var evt = MakeEvent();
        var cond = new RuleCondition("nonexistent", FilterOperator.Equals, "value");

        _engine.Evaluate(cond, evt).Should().BeFalse();
    }

    // --- EvaluateAll tests ---

    [Fact]
    public void EvaluateAll_RequireAll_AllMatch_ReturnsTrue()
    {
        var evt = MakeEvent(message: "hello", environment: "prod");
        var conditions = new[]
        {
            new RuleCondition("message", FilterOperator.Equals, "hello"),
            new RuleCondition("environment", FilterOperator.Equals, "prod"),
        };

        _engine.EvaluateAll(conditions, evt, requireAll: true).Should().BeTrue();
    }

    [Fact]
    public void EvaluateAll_RequireAll_OneFails_ReturnsFalse()
    {
        var evt = MakeEvent(message: "hello", environment: "prod");
        var conditions = new[]
        {
            new RuleCondition("message", FilterOperator.Equals, "hello"),
            new RuleCondition("environment", FilterOperator.Equals, "staging"),
        };

        _engine.EvaluateAll(conditions, evt, requireAll: true).Should().BeFalse();
    }

    [Fact]
    public void EvaluateAll_RequireAny_OneMatch_ReturnsTrue()
    {
        var evt = MakeEvent(message: "hello");
        var conditions = new[]
        {
            new RuleCondition("message", FilterOperator.Equals, "hello"),
            new RuleCondition("environment", FilterOperator.Equals, "staging"),
        };

        _engine.EvaluateAll(conditions, evt, requireAll: false).Should().BeTrue();
    }

    [Fact]
    public void EvaluateAll_RequireAny_NoneMatch_ReturnsFalse()
    {
        var evt = MakeEvent();
        var conditions = new[]
        {
            new RuleCondition("message", FilterOperator.Equals, "nope"),
            new RuleCondition("environment", FilterOperator.Equals, "nope"),
        };

        _engine.EvaluateAll(conditions, evt, requireAll: false).Should().BeFalse();
    }

    [Fact]
    public void EvaluateAll_EmptyConditions_RequireAll_ReturnsTrue()
    {
        var evt = MakeEvent();
        _engine.EvaluateAll([], evt, requireAll: true).Should().BeTrue();
    }

    [Fact]
    public void EvaluateAll_EmptyConditions_RequireAny_ReturnsFalse()
    {
        var evt = MakeEvent();
        _engine.EvaluateAll([], evt, requireAll: false).Should().BeFalse();
    }
}
