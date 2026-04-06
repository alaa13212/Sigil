using System.Text.Json;

namespace Sigil.Infrastructure.Integrations;

internal abstract class TrackerClientBase(IHttpClientFactory httpClientFactory)
{
    protected static readonly JsonSerializerOptions CamelCaseOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    protected HttpClient CreateClient() => httpClientFactory.CreateClient();

    protected static T ParseConfig<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, CamelCaseOptions)
        ?? throw new InvalidOperationException($"Invalid {typeof(T).Name} config.");
}
