namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
internal class SentryMessage
{
    public string? Message { get; set; }

    public string? Formatted { get; set; }

    public List<string>? Params { get; set; }
}