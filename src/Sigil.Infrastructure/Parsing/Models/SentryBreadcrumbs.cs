using System.Text.Json.Serialization;

namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
[JsonConverter(typeof(SentryBreadcrumbsValuesConverter))]
internal class SentryBreadcrumbs : ISentryValuesWrapper<SentryBreadcrumb>
{
    public List<SentryBreadcrumb>? Values { get; set; }
}
