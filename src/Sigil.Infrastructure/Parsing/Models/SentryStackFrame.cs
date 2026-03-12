using System.Text.Json.Serialization;

namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
internal class SentryStackFrame
{
    public string? Filename { get; set; }

    public string? Function { get; set; }
    public string? Module { get; set; }
    
    [JsonPropertyName("lineno")]
    public int? LineNumber { get; set; }
    
    [JsonPropertyName("colno")]
    public int? ColumnNumber { get; set; }
    
    public bool? InApp { get; set; }
    public bool? Native { get; set; }

    [JsonPropertyName("context_line")]
    public string? ContextLine { get; set; }

    [JsonPropertyName("pre_context")]
    public List<string>? PreContext { get; set; }

    [JsonPropertyName("post_context")]
    public List<string>? PostContext { get; set; }
}