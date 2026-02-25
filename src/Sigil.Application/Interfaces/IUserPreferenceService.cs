namespace Sigil.Application.Interfaces;

public interface IUserPreferenceService
{
    Task<string?> GetAsync(string key);
    Task SetAsync(string key, string value);
}
