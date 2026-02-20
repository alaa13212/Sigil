using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sigil.Infrastructure.Parsing.Models;

[Serializable]
internal class SentryUser
{
    public string? Id { get; set; }

    public string? Username { get; set; }

    public string? Email { get; set; }
    
    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }
    
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Data { get; set; }
}