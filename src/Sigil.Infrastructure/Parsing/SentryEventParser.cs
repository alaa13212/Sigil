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
        
        SentryException? primary = SelectPrimaryException(eventData.Exception?.Values);

        return new ParsedEvent
        {
            EventId = eventData.EventId,
            Timestamp = eventData.Timestamp.Value.ToUniversalTime(),
            Platform = PlatformHelper.Parse(eventData.Platform),
            Level = SeverityHelper.Parse(eventData.Level),
            ServerName = eventData.ServerName,
            Release = eventData.Release,

            ExceptionType = primary?.Type ?? eventData.Exception?.Values?.LastOrDefault()?.Type,
            Message = GetMessage(eventData, primary),
            Culprit = GetCulprit(eventData, primary),
            Environment = eventData.Environment,
            Logger = eventData.Logger,
            Runtime = GetRuntime(eventData),
            RawJson = rawJson,
            Extra = GetExtra(eventData),
            Tags = eventData.Tags,
            User = ConvertToParsedEventUser(eventData.User),
            FingerprintHints = eventData.Fingerprint,
            Stacktrace = GetStackFrames(eventData, primary)?
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

    /// <summary>
    /// Selects the primary exception using Sentry's mechanism metadata.
    /// Algorithm:
    /// 1. Find root: exception where mechanism.exception_id == 0, or fall back to last
    /// 2. Resolve groups: if mechanism.is_exception_group, find last child with matching parent_id, recurse
    /// 3. Handle synthetic: if mechanism.synthetic, substitute Value with last in-app frame's Function
    /// </summary>
    internal static SentryException? SelectPrimaryException(List<SentryException>? exceptions)
    {
        if (exceptions is null or { Count: 0 })
            return null;

        // If no exceptions have mechanism metadata, fall back to last
        if (exceptions.All(e => e.Mechanism is null))
            return exceptions.Last();

        // Find root: exception_id == 0, or fall back to last
        var root = exceptions.FirstOrDefault(e => e.Mechanism?.ExceptionId == 0)
                   ?? exceptions.Last();

        // Resolve exception groups: drill down into children
        var current = root;
        while (current.Mechanism?.IsExceptionGroup == true)
        {
            var parentId = current.Mechanism.ExceptionId;
            var lastChild = exceptions.LastOrDefault(e => e.Mechanism?.ParentId == parentId);
            if (lastChild == null || lastChild == current)
                break;
            current = lastChild;
        }

        // Handle synthetic exceptions: substitute Value with last in-app frame's Function
        if (current.Mechanism?.Synthetic == true && current.Stacktrace?.Frames is { Count: > 0 } frames)
        {
            var lastInAppFrame = frames.LastOrDefault(f => f.InApp == true);
            if (lastInAppFrame?.Function != null)
                current.Value = lastInAppFrame.Function;
        }

        return current;
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

    private static string? GetMessage(SentryEvent sentryEvent, SentryException? primary) => sentryEvent switch {
        { LogEntry.Formatted: not null, Threads.Values.Count: > 0 } => sentryEvent.LogEntry.Formatted,
        { LogEntry.Message: not null, Threads.Values.Count: > 0 } => sentryEvent.LogEntry.Message,
        { Message.Formatted: not null, Threads.Values.Count: > 0 } => sentryEvent.Message.Formatted,
        { Message.Message: not null, Threads.Values.Count: > 0 } => sentryEvent.Message.Message,
        { Exception.Values: [..] } when primary?.Value is not null => primary.Value,
        { Exception.Values: [.., var last] } => last.Value,
        _ => null
    };
    
    private static List<SentryStackFrame>? GetStackFrames(SentryEvent sentryEvent, SentryException? primary) => sentryEvent switch
    {
        { Exception.Values: [..] } when primary?.Stacktrace?.Frames is not null => primary.Stacktrace.Frames,
        { Exception.Values: [.., {Stacktrace: not null} last]  } => last.Stacktrace.Frames,
        { LogEntry.Formatted: not null, Threads.Values: [.., {Stacktrace: not null} last] } => last.Stacktrace.Frames,
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

    private static string? GetCulprit(SentryEvent sentryEvent, SentryException? primary)
    {
        if (primary?.Mechanism?.Synthetic == true)
            return null;
        
        var frames = GetStackFrames(sentryEvent, primary);
        if (frames.IsNullOrEmpty())
            return null;
        
        var culpritFrame = frames.LastOrDefault(frame => frame.InApp == true) ?? frames.Last();
        return $"{culpritFrame.Module} in {culpritFrame.Function}";
    }
}
