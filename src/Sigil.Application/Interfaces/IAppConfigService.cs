namespace Sigil.Application.Interfaces;

public interface IAppConfigService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string? value);
    Task<Dictionary<string, string?>> GetAllAsync();
}
