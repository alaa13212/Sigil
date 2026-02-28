using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigil.Infrastructure.Parsing.Models;

internal interface ISentryValuesWrapper<TItem>
{
    List<TItem>? Values { get; set; }
}

/// <summary>
/// Handles deserialization of Sentry "values" wrappers which can be in two formats:
/// 1. Direct array: [...]
/// 2. Wrapped object: {"values": [...]}
/// Used for breadcrumbs, exceptions, and threads.
/// </summary>
internal abstract class SentryValuesConverter<TWrapper, TItem> : JsonConverter<TWrapper?>
    where TWrapper : class, ISentryValuesWrapper<TItem>, new()
{
    public override TWrapper? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var items = JsonSerializer.Deserialize<List<TItem>>(ref reader, options);
            return new TWrapper { Values = items };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            // Parse the object manually to avoid infinite recursion
            // (calling Deserialize<TWrapper> would re-enter this converter)
            var wrapper = new TWrapper();
            using var doc = JsonDocument.ParseValue(ref reader);
            if (doc.RootElement.TryGetProperty("values", out var valuesElement))
            {
                wrapper.Values = valuesElement.Deserialize<List<TItem>>(options);
            }
            return wrapper;
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, TWrapper? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("values");
        JsonSerializer.Serialize(writer, value.Values, options);
        writer.WriteEndObject();
    }
}

internal sealed class SentryBreadcrumbsValuesConverter : SentryValuesConverter<SentryBreadcrumbs, SentryBreadcrumb>;
internal sealed class SentryExceptionDataValuesConverter : SentryValuesConverter<SentryExceptionData, SentryException>;
internal sealed class SentryThreadDataValuesConverter : SentryValuesConverter<SentryThreadData, SentryThread>;
