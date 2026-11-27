using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;

namespace Sigil.Server.Client.Services;

public class ApiCurrentUserService(HttpClient http) : ICurrentUserService
{
    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        var response = await http.GetAsync("api/account/me");
        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content.ReadFromJsonAsync<UserInfo>();
    }
}
