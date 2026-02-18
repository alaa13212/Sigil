using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Teams;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence;

internal class TeamService(SigilDbContext dbContext) : ITeamService
{
    public async Task<List<TeamResponse>> GetTeamsAsync()
    {
        return await dbContext.Teams
            .OrderBy(t => t.Name)
            .Select(t => new TeamResponse(t.Id, t.Name, t.Members.Count, t.Projects.Count))
            .ToListAsync();
    }

    public async Task<TeamDetailResponse?> GetTeamDetailAsync(int teamId)
    {
        var team = await dbContext.Teams
            .Include(t => t.Members).ThenInclude(m => m.User)
            .Include(t => t.Projects)
            .FirstOrDefaultAsync(t => t.Id == teamId);

        if (team is null) return null;

        var members = team.Members.Select(m =>
            new TeamMemberResponse(m.UserId, m.User.DisplayName, m.User.Email, m.Role)).ToList();

        var projects = team.Projects.Select(p =>
            new TeamProjectResponse(p.Id, p.Name, p.Platform)).ToList();

        return new TeamDetailResponse(team.Id, team.Name, members, projects);
    }

    public async Task<TeamResponse> CreateTeamAsync(string name, Guid creatorUserId)
    {
        var team = new Team { Name = name };
        dbContext.Teams.Add(team);

        var membership = new TeamMembership
        {
            Team = team,
            UserId = creatorUserId,
            Role = TeamRole.Owner
        };
        dbContext.TeamMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        return new TeamResponse(team.Id, team.Name, 1, 0);
    }

    public async Task<TeamResponse?> UpdateTeamAsync(int teamId, string name)
    {
        var team = await dbContext.Teams.AsTracking()
            .Include(t => t.Members)
            .Include(t => t.Projects)
            .FirstOrDefaultAsync(t => t.Id == teamId);

        if (team is null) return null;

        team.Name = name;
        await dbContext.SaveChangesAsync();

        return new TeamResponse(team.Id, team.Name, team.Members.Count, team.Projects.Count);
    }

    public async Task<bool> DeleteTeamAsync(int teamId)
    {
        return await dbContext.Teams.Where(t => t.Id == teamId).ExecuteDeleteAsync() > 0;
    }

    public async Task<bool> AddMemberAsync(int teamId, Guid userId, TeamRole role)
    {
        var exists = await dbContext.TeamMemberships
            .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId);
        if (exists) return false;

        var team = await dbContext.Teams.FindAsync(teamId);
        var user = await dbContext.Users.FindAsync(userId);
        if (team is null || user is null) return false;

        dbContext.TeamMemberships.Add(new TeamMembership
        {
            TeamId = teamId,
            Team = team,
            UserId = userId,
            User = user,
            Role = role
        });
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveMemberAsync(int teamId, Guid userId)
    {
        return await dbContext.TeamMemberships
            .Where(tm => tm.TeamId == teamId && tm.UserId == userId)
            .ExecuteDeleteAsync() > 0;
    }

    public async Task<bool> UpdateMemberRoleAsync(int teamId, Guid userId, TeamRole role)
    {
        var membership = await dbContext.TeamMemberships.AsTracking()
            .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == userId);
        if (membership is null) return false;

        membership.Role = role;
        await dbContext.SaveChangesAsync();
        return true;
    }
}
