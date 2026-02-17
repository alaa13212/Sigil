using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;

namespace Sigil.Server.Client.Services;

public class ApiSetupService(HttpClient http) : ISetupService
{
    public async Task<SetupStatus> GetSetupStatusAsync()
    {
        return await http.GetFromJsonAsync<SetupStatus>("api/setup/status")
               ?? new SetupStatus(false, 0);
    }

    public async Task<DbStatusResponse> GetDbStatusAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<DbStatusResponse>("api/setup/db-status")
                   ?? new DbStatusResponse(DbConnectionStatus.ConnectionFailed, "Failed to get status", [], []);
        }
        catch (Exception ex)
        {
            return new DbStatusResponse(DbConnectionStatus.ConnectionFailed, ex.Message, [], []);
        }
    }

    public async Task<bool> MigrateAsync()
    {
        var response = await http.PostAsync("api/setup/migrate", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<SetupResult> InitializeAsync(SetupRequest request)
    {
        var response = await http.PostAsJsonAsync("api/setup/initialize", request);
        if (!response.IsSuccessStatusCode)
            return SetupResult.Failure("Setup failed.");

        return await response.Content.ReadFromJsonAsync<SetupResult>()
               ?? SetupResult.Failure("Failed to parse setup response.");
    }
}
