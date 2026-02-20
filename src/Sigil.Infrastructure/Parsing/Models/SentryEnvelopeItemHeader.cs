namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
internal class SentryEnvelopeItemHeader
{
    public string? ContentType { get; set; }
    public string? Type { get; set; }
    public int Length { get; set; }
}