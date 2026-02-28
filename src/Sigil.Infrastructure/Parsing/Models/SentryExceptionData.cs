using System.Text.Json.Serialization;

namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
[JsonConverter(typeof(SentryExceptionDataValuesConverter))]
internal class SentryExceptionData : ISentryValuesWrapper<SentryException>
{
    public List<SentryException>? Values { get; set; }
}
