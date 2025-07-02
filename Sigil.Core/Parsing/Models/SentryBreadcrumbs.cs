namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryBreadcrumbs
{
    public List<SentryBreadcrumb>? Values { get; set; }
}