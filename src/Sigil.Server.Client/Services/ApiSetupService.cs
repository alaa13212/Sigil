using System.Net.Http.Json;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;

namespace Sigil.Server.Client.Services;

public class ApiSetupService(HttpClient http) : ISetupService
{
    public async Task<SetupStatus> GetSetupStatusAsync()
    {
        return await http.GetFromJsonAsync<SetupStatus>("api/setup/status")
               ?? new SetupStatus(false);
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

    public async Task<bool> HasPendingMigrationsAsync()
    {
        try
        {
            var result = await http.GetFromJsonAsync<HasPendingBody>("api/setup/maintenance/has-pending");
            return result?.HasPending ?? false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DbStatusResponse> GetMaintenanceDbStatusAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<DbStatusResponse>("api/setup/maintenance/db-status")
                   ?? new DbStatusResponse(DbConnectionStatus.ConnectionFailed, "Failed to get status", [], []);
        }
        catch (Exception ex)
        {
            return new DbStatusResponse(DbConnectionStatus.ConnectionFailed, ex.Message, [], []);
        }
    }

    public async Task ApplyPendingMigrationsAsync()
    {
        await http.PostAsync("api/setup/maintenance/migrate", null);
    }

    private record HasPendingBody(bool HasPending);
}
