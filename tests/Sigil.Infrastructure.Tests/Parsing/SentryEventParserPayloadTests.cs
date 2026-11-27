using System.Text.Json;
using Sigil.Domain.Ingestion;
using Sigil.Infrastructure.Parsing;

namespace Sigil.Infrastructure.Tests.Parsing;

public class SentryEventParserPayloadTests
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static EventParsingContext MakeContext() => new()
    {
        ProjectId = 1,
        NormalizationRules = [],
        AutoTagRules = [],
        InboundFilters = [],
        StackTraceFilters = [],
    };

    private static async Task<ParsedEvent> ParseAsync(string payloadFragment)
    {
        string payload = $$"""
            {"event_id":"{{Guid.NewGuid():N}}","timestamp":"2026-07-01T12:00:00Z","platform":"csharp"{{payloadFragment}}}
            """;

        string envelope = $$"""
            {"event_id":"{{Guid.NewGuid():N}}","sent_at":"2026-07-01T12:00:00Z"}
            {"type":"event","length":{{payload.Length}}}
            {{payload}}
            """;

        var parser = new SentryEventParser([], SnakeCaseOptions);
        var events = await parser.Parse(MakeContext(), envelope, DateTime.UtcNow);

        events.Should().HaveCount(1);
        return events[0];
    }

    [Fact]
    public async Task Parse_EmptyContexts_RuntimeIsNull()
    {
        var parsed = await ParseAsync(",\"contexts\":{}");

        parsed.Runtime.Should().BeNull();
    }

    [Fact]
    public async Task Parse_ContextsWithoutRuntimeKey_RuntimeIsNull()
    {
        var parsed = await ParseAsync(",\"contexts\":{\"os\":{\"name\":\"Android\"}}");

        parsed.Runtime.Should().BeNull();
    }

    [Fact]
    public async Task Parse_RuntimeWithoutVersion_KeepsNameAndEmptyVersion()
    {
        var parsed = await ParseAsync(",\"contexts\":{\"runtime\":{\"name\":\"Unity\"}}");

        parsed.Runtime.Should().NotBeNull();
        parsed.Runtime!.Name.Should().Be("Unity");
        parsed.Runtime.Version.Should().BeEmpty();
    }

    [Fact]
    public async Task Parse_RuntimeWithNameAndVersion_KeepsBoth()
    {
        var parsed = await ParseAsync(",\"contexts\":{\"runtime\":{\"name\":\".NET\",\"version\":\"10.0.0\"}}");

        parsed.Runtime.Should().NotBeNull();
        parsed.Runtime!.Name.Should().Be(".NET");
        parsed.Runtime.Version.Should().Be("10.0.0");
    }

    [Fact]
    public async Task Parse_RuntimeWithNumericVersion_KeepsNameAndNumericVersion()
    {
        var parsed = await ParseAsync(",\"contexts\":{\"runtime\":{\"name\":\"Unity\",\"version\":6000}}");

        parsed.Runtime.Should().NotBeNull();
        parsed.Runtime!.Name.Should().Be("Unity");
        parsed.Runtime.Version.Should().Be("6000");
    }

    [Fact]
    public async Task Parse_RuntimeThatIsNotAnObject_RuntimeIsNull()
    {
        var parsed = await ParseAsync(",\"contexts\":{\"runtime\":\"Unity\"}");

        parsed.Runtime.Should().BeNull();
    }

    [Fact]
    public async Task Parse_RuntimeWithoutName_RuntimeIsNull()
    {
        var parsed = await ParseAsync(",\"contexts\":{\"runtime\":{\"version\":\"10.0.0\"}}");

        parsed.Runtime.Should().BeNull();
    }

    [Fact]
    public async Task Parse_RuntimeWithNullVersion_KeepsNameAndEmptyVersion()
    {
        var parsed = await ParseAsync(",\"contexts\":{\"runtime\":{\"name\":\"Unity\",\"version\":null}}");

        parsed.Runtime.Should().NotBeNull();
        parsed.Runtime!.Name.Should().Be("Unity");
        parsed.Runtime.Version.Should().BeEmpty();
    }

    [Fact]
    public async Task Parse_ExtraWithNonStringValues_ConvertsEachValueToText()
    {
        var parsed = await ParseAsync(",\"extra\":{\"count\":3,\"ok\":true,\"obj\":{\"a\": 1},\"s\":\"x\"}");

        parsed.Extra.Should().NotBeNull();
        parsed.Extra!["count"].Should().Be("3");
        parsed.Extra["ok"].Should().Be("true");
        parsed.Extra["obj"].Should().Be("{\"a\":1}");
        parsed.Extra["s"].Should().Be("x");
    }

    [Fact]
    public async Task Parse_ExtraWithArrayValue_ConvertsValueToCompactJson()
    {
        var parsed = await ParseAsync(",\"extra\":{\"items\":[1, 2]}");

        parsed.Extra.Should().NotBeNull();
        parsed.Extra!["items"].Should().Be("[1,2]");
    }

    [Fact]
    public async Task Parse_UserDataWithNonStringValues_ConvertsEachValueToText()
    {
        var parsed = await ParseAsync(",\"user\":{\"id\":\"u1\",\"segment\":7}");

        parsed.User.Should().NotBeNull();
        parsed.User!.Id.Should().Be("u1");
        parsed.User.Data.Should().NotBeNull();
        parsed.User.Data!["segment"].Should().Be("7");
    }
}
