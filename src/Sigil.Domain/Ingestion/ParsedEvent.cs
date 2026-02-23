using Sigil.Domain.Enums;

namespace Sigil.Domain.Ingestion;

public class ParsedEvent 
{
    public required string EventId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required Platform Platform { get; init; }
    public required Severity Level { get; init; }
    public string? ServerName { get; init; }
    public string? Release { get; init; }
    public string? ExceptionType { get; init; }
    public string? Message { get; init; }
    public string? NormalizedMessage { get; set; }
    public string? Fingerprint { get; set; }
    public DateTime ReceivedAt { get; set; }
    
    public string? Culprit { get; init; }
    public string? Environment { get; init; }
    
    public string? Logger { get; init; }
    public Runtime? Runtime { get; init; }
    
    public required string RawJson { get; init; }
    
    public Dictionary<string, string>? Extra { get; init; }
    public Dictionary<string, string>? Tags { get; set; }
    
    public ParsedEventUser? User { get; set; }
    
    public IReadOnlyList<string>? FingerprintHints { get; init; }
    public IReadOnlyList<ParsedStackFrame> Stacktrace { get; init; } = [];
    
}