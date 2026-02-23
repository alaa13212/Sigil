namespace Sigil.Domain.Ingestion;

public class ParsedStackFrame
{
    public string? Filename { get; init; }
    public string? Function { get; init; }
    public string? Module { get; init; }
    public int? LineNumber { get; init; }
    public int? ColumnNumber { get; init; }
    public bool InApp { get; init; }
    
}