using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Digestion;

namespace Sigil.Server.Client.Services;

internal class ApiDigestionMonitorService(HttpClient http) : IDigestionMonitorService
{
    public async Task<DigestionStats> GetStatsAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<DigestionStats>("api/admin/digestion/stats") ?? new DigestionStats();
        }
        catch
        {
            return new DigestionStats();
        }
    }

    public async Task<List<FailedEnvelopeSummary>> GetRecentFailuresAsync(int limit = 50)
    {
        try
        {
            return await http.GetFromJsonAsync<List<FailedEnvelopeSummary>>($"api/admin/digestion/failures?limit={limit}") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<int> RetryFailedAsync(IEnumerable<long>? ids = null)
    {
        var idList = ids?.ToList();
        var response = await http.PostAsJsonAsync("api/admin/digestion/retry", new { ids = idList });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<RetryResponse>();
        return result?.RetriedCount ?? 0;
    }

    private record RetryResponse(int RetriedCount);
}
