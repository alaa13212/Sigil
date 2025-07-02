using System.Text.Json.Serialization;

namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryStackFrame
{
    public string? Filename { get; set; }

    public string? Function { get; set; }
    public string? Module { get; set; }
    
    [JsonPropertyName("lineno")]
    public int? LineNumber { get; set; }
    
    public bool? InApp { get; set; }
    public bool? Native { get; set; }
    
}