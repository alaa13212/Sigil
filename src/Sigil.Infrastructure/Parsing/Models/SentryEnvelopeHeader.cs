namespace Sigil.infrastructure.Parsing.Models;

[Serializable]
internal class SentryEnvelopeHeader
{
    public string? EventId { get; set; }
    public SentrySdkInfo? Sdk { get; set; }
}