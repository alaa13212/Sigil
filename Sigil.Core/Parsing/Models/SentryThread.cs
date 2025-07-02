namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryThread
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? State { get; set; }
    public bool Crashed { get; set; }
    public bool Daemon { get; set; }

    public SentryStacktrace? Stacktrace { get; set; }
}