using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Application.Interfaces;

public interface IProjectService
{
    Task<Project> CreateProjectAsync(string name, Platform platform);
    Task<Project?> GetProjectByIdAsync(int id);
}
