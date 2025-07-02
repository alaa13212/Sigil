namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentrySdkInfo
{
    public string? Name { get; set; }

    public string? Version { get; set; }

    public List<string>? Integrations { get; set; }

    public List<SentryPackage>? Packages { get; set; }
}