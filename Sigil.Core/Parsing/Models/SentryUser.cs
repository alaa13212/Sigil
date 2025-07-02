using System.Text.Json;

namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryUser
{
    public string? Id { get; set; }

    public string? Username { get; set; }

    public string? Email { get; set; }

    public string? IpAddress { get; set; }

    public string? Segment { get; set; }

    public Dictionary<string, JsonElement>? Data { get; set; }
}