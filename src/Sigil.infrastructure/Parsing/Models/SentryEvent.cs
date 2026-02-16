using System.Text.Json;

namespace Sigil.infrastructure.Parsing.Models;

[Serializable]
internal class SentryEvent
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
    
}