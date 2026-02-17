using Sigil.Application.Models.Auth;

namespace Sigil.Application.Interfaces;

public interface ISetupService
{
    Task<SetupStatus> GetSetupStatusAsync();
    Task<DbStatusResponse> GetDbStatusAsync();
    Task<bool> MigrateAsync();
    Task<SetupResult> InitializeAsync(SetupRequest request);
}
