namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
internal class SentryMechanism
{
    public string? Type { get; set; }
    public int? ExceptionId { get; set; }
    public bool IsExceptionGroup { get; set; }
    public int? ParentId { get; set; }
    public bool Synthetic { get; set; }
    public string? Source { get; set; }
}
