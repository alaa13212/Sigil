namespace Sigil.infrastructure.Parsing.Models;

[Serializable]
internal class SentrySdkInfo
{
    public string? Name { get; set; }

    public string? Version { get; set; }

    public List<string>? Integrations { get; set; }

    public List<SentryPackage>? Packages { get; set; }
}