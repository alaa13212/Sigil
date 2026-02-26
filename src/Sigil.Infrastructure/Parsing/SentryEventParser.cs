using System.Text.Json;
using Sigil.Domain.Enums;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;
using Sigil.Infrastructure.Parsing.Models;

namespace Sigil.Infrastructure.Parsing;

internal class SentryEventParser(IEnumerable<IEventEnricher> enrichers, JsonSerializerOptions jsonSerializerOptions) : IEventParser
{
    public async Task<List<ParsedEvent>> Parse(EventParsingContext context, string rawEnvelope, DateTime receivedAt)
    {
        using var reader = new StringReader(rawEnvelope);
        string envelopeHeaderLine = (await reader.ReadLineAsync())!;

        // Envelope header is not needed
        _ = JsonSerializer.Deserialize<SentryEnvelopeHeader>(envelopeHeaderLine, jsonSerializerOptions);

        List<ParsedEvent> events = [];
        while (await reader.ReadLineAsync() is { } itemHeaderLine)
        {
            var itemHeader = JsonSerializer.Deserialize<SentryEnvelopeItemHeader>(itemHeaderLine, jsonSerializerOptions)!;
            string payloadLine = (await reader.ReadLineAsync())!;

            if (itemHeader.Type == "event")
            {
                var eventData = JsonSerializer.Deserialize<SentryEvent>(payloadLine, jsonSerializerOptions)!;
                ParsedEvent parsedEvent = ConvertToParsedEvent(eventData, payloadLine);
                parsedEvent.ReceivedAt = receivedAt;

                foreach (var enricher in enrichers)
                    enricher.Enrich(parsedEvent, context);

                events.Add(parsedEvent);
            }
        }

        return events;
    }

    private ParsedEvent ConvertToParsedEvent(SentryEvent eventData, string rawJson)
    {
        // These are required as per sentry documentation
        ArgumentNullException.ThrowIfNull(eventData.EventId);
        ArgumentNullException.ThrowIfNull(eventData.Timestamp);
        ArgumentNullException.ThrowIfNull(eventData.Platform);
        
        return new ParsedEvent
        {
            EventId = eventData.EventId,
            Timestamp = eventData.Timestamp.Value.ToUniversalTime(),
            Platform = PlatformHelper.Parse(eventData.Platform),
            Level = SeverityHelper.Parse(eventData.Level),
            ServerName = eventData.ServerName,
            Release = eventData.Release,
            
            ExceptionType = eventData.Exception?.Values?.LastOrDefault()?.Type,
            Message = GetMessage(eventData),
            Culprit = GetCulprit(eventData),
            Environment = eventData.Environment,
            Logger = eventData.Logger,
            Runtime = GetRuntime(eventData),
            RawJson = rawJson,
            Extra = GetExtra(eventData),
            Tags = eventData.Tags,
            User = ConvertToParsedEventUser(eventData.User),
            FingerprintHints = eventData.Fingerprint,
            Stacktrace = GetStackFrames(eventData)?
            .Select(f => new ParsedStackFrame
            {
                Filename = f.Filename,
                Function = f.Function,
                Module = f.Module,
                LineNumber = f.LineNumber,
                ColumnNumber = f.ColumnNumber,
                InApp = f.InApp == true
            })
            .Reverse()
            .ToList() ?? [],
            
        };
    }

    private ParsedEventUser? ConvertToParsedEventUser(SentryUser? eventDataUser)
    {
        if (eventDataUser?.Id == null && eventDataUser?.Username == null && eventDataUser?.Email == null && eventDataUser?.IpAddress == null)
            return null;
        
        return new ParsedEventUser
        {
            Id = eventDataUser.Id,
            Username = eventDataUser.Username,
            Email = eventDataUser.Email,
            IpAddress = eventDataUser.IpAddress,
            Data = eventDataUser.Data?.ToDictionary(pair => pair.Key, pair => pair.Value.GetString()!),
        };
    }

    private static string? GetMessage(SentryEvent sentryEvent) => sentryEvent switch {
        { Message.Formatted: not null, Threads.Values.Count: > 0 } => sentryEvent.Message.Formatted, // TODO Format
        { Message.Message: not null, Threads.Values.Count: > 0 } => sentryEvent.Message.Message,
        { Exception.Values: [.., var last] } => last.Value,
        _ => null
    };
    
    private static List<SentryStackFrame>? GetStackFrames(SentryEvent sentryEvent) => sentryEvent switch
    {
        { Exception.Values: [.., {Stacktrace: not null} last]  } => last.Stacktrace.Frames,
        { Message.Formatted: not null, Threads.Values: [.., {Stacktrace: not null} last] } => last.Stacktrace.Frames,
        _ => null,
    };

    private static Runtime? GetRuntime(SentryEvent sentryEvent)
    {
        var pair = sentryEvent.Contexts?.FirstOrDefault(pair => pair.Key == "runtime");
        if(pair == null) return null;

        string? name = pair.Value.Value.GetProperty("name").GetString();
        string? version = pair.Value.Value.GetProperty("version").GetString();
        if(name == null || version == null) return null;
        return new Runtime(name,version);
    }
    
    private static Dictionary<string, string>? GetExtra(SentryEvent sentryEvent)
    {
        return sentryEvent.Extra?.ToDictionary(pair => pair.Key, pair => pair.Value.GetString()!);
    }

    private static string? GetCulprit(SentryEvent sentryEvent)
    {
        var frames = GetStackFrames(sentryEvent);
        if (frames.IsNullOrEmpty())
            return null;
        
        var culpritFrame = frames.LastOrDefault(frame => frame.InApp == true) ?? frames.Last();
        return $"{culpritFrame.Module} in {culpritFrame.Function}";
    }
}