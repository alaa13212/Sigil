using System.Text;
using System.Text.Json;
using Sigil.Core.Parsing.Models;

namespace Sigil.Core.Parsing;

public class SentryEventParser(JsonSerializerOptions jsonSerializerOptions) : IEventParser
{
    public async Task<List<SentryEvent>> Parse(Stream envelopeStream)
    {
        using var reader = new StreamReader(envelopeStream, Encoding.UTF8);
        string envelopeHeaderLine = (await reader.ReadLineAsync())!;
        
        // Envelope header is not needed
        _ = JsonSerializer.Deserialize<SentryEnvelopeHeader>(envelopeHeaderLine, jsonSerializerOptions);

        List<SentryEvent> events = [];
        while (await reader.ReadLineAsync() is { } itemHeaderLine)
        {
            var itemHeader = JsonSerializer.Deserialize<SentryEnvelopeItemHeader>(itemHeaderLine, jsonSerializerOptions)!;
            string payloadLine = (await reader.ReadLineAsync())!;

            if (itemHeader.Type == "event")
            {
                var eventData = JsonSerializer.Deserialize<SentryEvent>(payloadLine, jsonSerializerOptions)!;
                eventData.RawJson = payloadLine;
                events.Add(eventData);
            }
        }

        return events;
    }
}