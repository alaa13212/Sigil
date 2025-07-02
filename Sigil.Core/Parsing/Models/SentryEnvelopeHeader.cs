namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryEnvelopeHeader
{
    public string? EventId { get; set; }
    public SentrySdkInfo? Sdk { get; set; }
}