using Sigil.Domain.Enums;

namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Resolves user roles for authorization checks.</summary>
public interface ITeamRoleService
{
    Task<TeamRole?> GetUserRoleForProjectAsync(Guid userId, int projectId);
    Task<TeamRole?> GetUserRoleForTeamAsync(Guid userId, int teamId);
}
