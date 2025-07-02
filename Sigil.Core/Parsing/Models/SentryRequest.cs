namespace Sigil.Core.Parsing.Models;

[Serializable]
public class SentryRequest
{
    public string? Url { get; set; }

    public string? Method { get; set; }

    public Dictionary<string, string>? Headers { get; set; }

    public string? Cookies { get; set; }

    public string? QueryString { get; set; }

    public object? Data { get; set; }

    public Dictionary<string, string>? Env { get; set; }
}