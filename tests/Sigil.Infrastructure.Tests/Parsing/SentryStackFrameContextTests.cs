using System.Text.Json;
using Sigil.Infrastructure.Parsing.Models;

namespace Sigil.Infrastructure.Tests.Parsing;

public class SentryStackFrameContextTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SentryStackFrame_DeserializesContextFields()
    {
        const string json = """
            {
                "filename": "src/app/handler.py",
                "function": "handle_request",
                "lineno": 42,
                "context_line": "    result = process(data)",
                "pre_context": [
                    "    if data is None:",
                    "        return None",
                    "    data = normalize(data)"
                ],
                "post_context": [
                    "    log.info('processed')",
                    "    return result"
                ]
            }
            """;

        var frame = JsonSerializer.Deserialize<SentryStackFrame>(json, JsonOptions);

        frame.Should().NotBeNull();
        frame!.ContextLine.Should().Be("    result = process(data)");
        frame.PreContext.Should().HaveCount(3);
        frame.PreContext![0].Should().Be("    if data is None:");
        frame.PreContext[1].Should().Be("        return None");
        frame.PreContext[2].Should().Be("    data = normalize(data)");
        frame.PostContext.Should().HaveCount(2);
        frame.PostContext![0].Should().Be("    log.info('processed')");
        frame.PostContext[1].Should().Be("    return result");
    }

    [Fact]
    public void SentryStackFrame_NullContextFields_WhenAbsent()
    {
        const string json = """
            {
                "filename": "src/app/handler.py",
                "function": "handle_request",
                "lineno": 42
            }
            """;

        var frame = JsonSerializer.Deserialize<SentryStackFrame>(json, JsonOptions);

        frame.Should().NotBeNull();
        frame!.ContextLine.Should().BeNull();
        frame.PreContext.Should().BeNull();
        frame.PostContext.Should().BeNull();
    }

    [Fact]
    public void SentryStackFrame_EmptyPreAndPostContext_DeserializesAsEmptyLists()
    {
        const string json = """
            {
                "filename": "src/app/handler.py",
                "function": "handle_request",
                "lineno": 1,
                "context_line": "def handle_request():",
                "pre_context": [],
                "post_context": []
            }
            """;

        var frame = JsonSerializer.Deserialize<SentryStackFrame>(json, JsonOptions);

        frame.Should().NotBeNull();
        frame!.ContextLine.Should().Be("def handle_request():");
        frame.PreContext.Should().NotBeNull().And.BeEmpty();
        frame.PostContext.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void SentryStackFrame_ContextLineOnly_PreAndPostAreNull()
    {
        const string json = """
            {
                "function": "myFunc",
                "lineno": 10,
                "context_line": "throw new Error('oops')"
            }
            """;

        var frame = JsonSerializer.Deserialize<SentryStackFrame>(json, JsonOptions);

        frame.Should().NotBeNull();
        frame!.ContextLine.Should().Be("throw new Error('oops')");
        frame.PreContext.Should().BeNull();
        frame.PostContext.Should().BeNull();
    }
}
