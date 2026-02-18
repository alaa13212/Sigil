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
}
