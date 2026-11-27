using Sigil.Application.Models.Projects;

namespace Sigil.Application.Interfaces;

public interface IProjectLimitsService
{
    Task<ProjectLimitsResponse> GetAsync(int projectId);
}
