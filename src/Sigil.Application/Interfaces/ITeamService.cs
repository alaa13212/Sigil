using Sigil.Application.Models.Teams;
using Sigil.Domain.Enums;

namespace Sigil.Application.Interfaces;

public interface ITeamService
{
    Task<List<TeamResponse>> GetTeamsAsync();
    Task<TeamDetailResponse?> GetTeamDetailAsync(int teamId);
    Task<TeamResponse> CreateTeamAsync(string name, Guid creatorUserId);
    Task<TeamResponse?> UpdateTeamAsync(int teamId, string name);
    Task<bool> DeleteTeamAsync(int teamId);
    Task<bool> AddMemberAsync(int teamId, Guid userId, TeamRole role);
    Task<bool> RemoveMemberAsync(int teamId, Guid userId);
    Task<bool> UpdateMemberRoleAsync(int teamId, Guid userId, TeamRole role);
    Task<TeamRole?> GetUserRoleForProjectAsync(Guid userId, int projectId);
    Task<TeamRole?> GetUserRoleForTeamAsync(Guid userId, int teamId);
}
