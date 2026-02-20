using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigil.Infrastructure.Parsing.Models;

/// <summary>
/// Handles deserialization of Sentry breadcrumbs which can be in two formats:
/// 1. Direct array: {"breadcrumbs": [...]}
/// 2. Wrapped object: {"breadcrumbs": {"values": [...]}}
/// </summary>
internal class SentryBreadcrumbsConverter : JsonConverter<SentryBreadcrumbs?>
{
    public override SentryBreadcrumbs? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        // Check if it's an array (direct format)
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var breadcrumbs = JsonSerializer.Deserialize<List<SentryBreadcrumb>>(ref reader, options);
            return new SentryBreadcrumbs { Values = breadcrumbs };
        }

        // Otherwise it's an object (wrapped format with "values" property)
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return JsonSerializer.Deserialize<SentryBreadcrumbs>(ref reader, options);
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, SentryBreadcrumbs? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        // Write in wrapped format
        JsonSerializer.Serialize(writer, value, options);
    }
}