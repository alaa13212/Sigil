namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryMessage
{
    public string? Message { get; set; }

    public string? Formatted { get; set; }

    public List<string>? Params { get; set; }
}