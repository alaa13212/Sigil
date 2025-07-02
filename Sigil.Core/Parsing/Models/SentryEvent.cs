using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryEvent
{
    public string? EventId { get; set; }

    public DateTime? Timestamp { get; set; }

    public string? Platform { get; set; }

    public string? Logger { get; set; }

    public string? Level { get; set; }

    public string? Transaction { get; set; }

    public string? ServerName { get; set; }

    public string? Release { get; set; }

    public string? Environment { get; set; }

    public SentryMessage? Message { get; set; }

    public SentryExceptionData? Exception { get; set; }
    public SentryThreadData? Threads { get; set; }

    public SentryStacktrace? Stacktrace { get; set; }

    public SentryUser? User { get; set; }

    public Dictionary<string, string>? Tags { get; set; }

    public Dictionary<string, JsonElement>? Extra { get; set; }

    public Dictionary<string, JsonElement>? Contexts { get; set; }

    public SentryBreadcrumbs? Breadcrumbs { get; set; }

    public List<string>? Fingerprint { get; set; }

    public SentrySdkInfo? Sdk { get; set; }

    public SentryRequest? Request { get; set; }

    [JsonIgnore]
    public string? RawJson { get; set; }

    public bool IsException() => Exception != null;
    public bool IsMessage() => Message != null;


    public string? GetMessage() => this switch {
        { Exception.Values.Count: > 0 } => Exception.Values.Last().Value,
        { Message.Formatted: not null, Threads.Values.Count: > 0 } => Message.Formatted,
        _ => null
    };
    
    public List<SentryStackFrame>? GetStackFrames() => this switch
    {
        { Exception.Values.Count: > 0 } => Exception.Values.Last().Stacktrace!.Frames,
        { Message.Formatted: not null, Threads.Values.Count: > 0 } => Threads!.Values.Last().Stacktrace!.Frames,
        _ => null,
    };
}