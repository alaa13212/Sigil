using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;

namespace Sigil.Server.Client.Services;

public class ApiBadgeService(HttpClient http) : IBadgeService
{
    public async Task<Dictionary<int, ProjectBadgeCounts>> GetAllBadgeCountsAsync(Guid userId)
    {
        return await http.GetFromJsonAsync<Dictionary<int, ProjectBadgeCounts>>("api/badges") ?? [];
    }
}
