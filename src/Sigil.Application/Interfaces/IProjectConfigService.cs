namespace Sigil.Application.Interfaces;

public interface IProjectConfigService
{
    Task<string?> GetAsync(int projectId, string key);
    Task SetAsync(int projectId, string key, string? value);
    Task<Dictionary<string, string?>> GetAllAsync(int projectId);
}
