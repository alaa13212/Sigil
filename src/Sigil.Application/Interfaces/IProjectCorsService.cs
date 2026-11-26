using Sigil.Application.Models.Projects;

namespace Sigil.Application.Interfaces;

public interface IProjectCorsService
{
    Task<IReadOnlyList<string>> GetOriginsAsync(int projectId);
    Task<IReadOnlyList<string>> SetOriginsAsync(int projectId, SetCorsOriginsRequest request);
}
