namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
internal class SentryStacktrace
{
    public bool Snapshot { get; set; }
    public List<SentryStackFrame>? Frames { get; set; }
}