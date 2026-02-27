namespace Sigil.Application.Interfaces;

public interface IAppConfigEditorService
{
    Task<Dictionary<string, string?>> GetAllAsync();
    Task SetAsync(string key, string? value);
}
