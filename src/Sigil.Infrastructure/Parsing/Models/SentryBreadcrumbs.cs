using System.Text.Json.Serialization;

namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
[JsonConverter(typeof(SentryBreadcrumbsConverter))]
internal class SentryBreadcrumbs
{
    public List<SentryBreadcrumb>? Values { get; set; }
}