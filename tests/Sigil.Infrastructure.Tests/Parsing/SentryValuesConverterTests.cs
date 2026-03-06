using System.Text.Json;
using Sigil.Infrastructure.Parsing.Models;

namespace Sigil.Infrastructure.Tests.Parsing;

public class SentryValuesConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // --- SentryExceptionData ---

    [Fact]
    public void ExceptionData_ObjectFormat_Deserializes()
    {
        var json = """{"values":[{"type":"NullRef","value":"msg"}]}""";

        var result = JsonSerializer.Deserialize<SentryExceptionData>(json, Options);

        result.Should().NotBeNull();
        result.Values.Should().HaveCount(1);
        result.Values![0].Type.Should().Be("NullRef");
    }

    [Fact]
    public void ExceptionData_ArrayFormat_Deserializes()
    {
        var json = """[{"type":"NullRef","value":"msg"}]""";

        var result = JsonSerializer.Deserialize<SentryExceptionData>(json, Options);

        result.Should().NotBeNull();
        result.Values.Should().HaveCount(1);
        result.Values![0].Type.Should().Be("NullRef");
    }

    [Fact]
    public void ExceptionData_Null_DeserializesNull()
    {
        var json = "null";

        var result = JsonSerializer.Deserialize<SentryExceptionData>(json, Options);

        result.Should().BeNull();
    }

    [Fact]
    public void ExceptionData_EmptyArray_DeserializesEmpty()
    {
        var json = "[]";

        var result = JsonSerializer.Deserialize<SentryExceptionData>(json, Options);

        result.Should().NotBeNull();
        result.Values.Should().BeEmpty();
    }

    [Fact]
    public void ExceptionData_Serializes_AsObject()
    {
        var data = new SentryExceptionData
        {
            Values = [new SentryException { Type = "Error", Value = "test" }]
        };

        var json = JsonSerializer.Serialize(data, Options);

        json.Should().Contain("\"values\"");
    }

    // --- SentryThreadData ---

    [Fact]
    public void ThreadData_ObjectFormat_Deserializes()
    {
        var json = """{"values":[{"id":1}]}""";

        var result = JsonSerializer.Deserialize<SentryThreadData>(json, Options);

        result.Should().NotBeNull();
        result.Values.Should().HaveCount(1);
    }

    [Fact]
    public void ThreadData_ArrayFormat_Deserializes()
    {
        var json = """[{"id":1}]""";

        var result = JsonSerializer.Deserialize<SentryThreadData>(json, Options);

        result.Should().NotBeNull();
        result.Values.Should().HaveCount(1);
    }

    // --- SentryBreadcrumbs ---

    [Fact]
    public void Breadcrumbs_ObjectFormat_Deserializes()
    {
        var json = """{"values":[{"message":"hello"}]}""";

        var result = JsonSerializer.Deserialize<SentryBreadcrumbs>(json, Options);

        result.Should().NotBeNull();
        result.Values.Should().HaveCount(1);
    }

    [Fact]
    public void Breadcrumbs_ArrayFormat_Deserializes()
    {
        var json = """[{"message":"hello"}]""";

        var result = JsonSerializer.Deserialize<SentryBreadcrumbs>(json, Options);

        result.Should().NotBeNull();
        result.Values.Should().HaveCount(1);
    }

    // --- Edge cases ---

    [Fact]
    public void ExceptionData_UnexpectedToken_Throws()
    {
        var json = "42";

        var act = () => JsonSerializer.Deserialize<SentryExceptionData>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void ExceptionData_ObjectWithoutValues_ReturnsEmptyValues()
    {
        var json = """{"other":"field"}""";

        var result = JsonSerializer.Deserialize<SentryExceptionData>(json, Options);

        result.Should().NotBeNull();
        result.Values.Should().BeNull();
    }

    [Fact]
    public void ExceptionData_Serialize_Null_WritesNull()
    {
        var json = JsonSerializer.Serialize<SentryExceptionData?>(null, Options);

        json.Should().Be("null");
    }
}
