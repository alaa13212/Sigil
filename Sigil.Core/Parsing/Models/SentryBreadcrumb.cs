using System.Text.Json;

namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryBreadcrumb
{
    public DateTime? Timestamp { get; set; }

    public string? Message { get; set; }

    public string? Category { get; set; }

    public Dictionary<string, JsonElement>? Data { get; set; }

    public string? Level { get; set; }

    public string? Type { get; set; }
}