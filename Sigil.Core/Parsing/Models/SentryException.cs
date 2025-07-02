namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryException
{
    public string? Type { get; set; }
    public string? Value { get; set; }
    public string? Module { get; set; }
    public int ThreadId { get; set; }

    public SentryStacktrace? Stacktrace { get; set; }
}