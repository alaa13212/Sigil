using System.Text.Json.Serialization;

namespace Sigil.infrastructure.Parsing.Models;

[Serializable]
[JsonConverter(typeof(SentryBreadcrumbsConverter))]
internal class SentryBreadcrumbs
{
    public List<SentryBreadcrumb>? Values { get; set; }
}