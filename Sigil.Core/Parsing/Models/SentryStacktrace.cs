namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryStacktrace
{
    public bool Snapshot { get; set; }
    public List<SentryStackFrame>? Frames { get; set; }
}