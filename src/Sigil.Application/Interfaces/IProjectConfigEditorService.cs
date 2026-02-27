namespace Sigil.Application.Interfaces;

public interface IProjectConfigEditorService
{
    Task<Dictionary<string, string?>> GetAllAsync(int projectId);
    Task SetAsync(int projectId, string key, string? value);
}
