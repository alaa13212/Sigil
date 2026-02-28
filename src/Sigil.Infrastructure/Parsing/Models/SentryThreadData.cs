using System.Text.Json.Serialization;

namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
[JsonConverter(typeof(SentryThreadDataValuesConverter))]
internal class SentryThreadData : ISentryValuesWrapper<SentryThread>
{
    public List<SentryThread>? Values { get; set; }
}
