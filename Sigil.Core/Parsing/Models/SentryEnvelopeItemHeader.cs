namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryEnvelopeItemHeader
{
    public string? ContentType { get; set; }
    public string? Type { get; set; }
    public int Length { get; set; }
}