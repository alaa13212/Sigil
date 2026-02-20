namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
internal class SentryExceptionData
{
    public List<SentryException>? Values { get; set; }
}