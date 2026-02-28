using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Alerts;

namespace Sigil.Server.Client.Services;

public class ApiAlertChannelService(HttpClient http) : IAlertChannelService
{
    public async Task<List<AlertChannelResponse>> GetAllChannelsAsync() =>
        await http.GetFromJsonAsync<List<AlertChannelResponse>>("api/alert-channels") ?? [];

    public async Task<AlertChannelResponse> CreateChannelAsync(CreateAlertChannelRequest request)
    {
        var response = await http.PostAsJsonAsync("api/alert-channels", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AlertChannelResponse>())!;
    }

    public async Task<AlertChannelResponse?> UpdateChannelAsync(int channelId, UpdateAlertChannelRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/alert-channels/{channelId}", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AlertChannelResponse>();
    }

    public async Task<bool> DeleteChannelAsync(int channelId)
    {
        var response = await http.DeleteAsync($"api/alert-channels/{channelId}");
        return response.IsSuccessStatusCode;
    }
}
