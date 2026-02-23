namespace Sigil.Domain.Ingestion;

public record Runtime(string Name, string Version)
{
    public override string ToString() => $"{Name} {Version}";
};