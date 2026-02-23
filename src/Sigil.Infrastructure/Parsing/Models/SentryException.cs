namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
internal class SentryException
{
    public string? Type { get; set; }
    public string? Value { get; set; }
    public string? Module { get; set; }
    public int ThreadId { get; set; }

    public SentryStacktrace? Stacktrace { get; set; }
}