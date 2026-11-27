using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;

namespace Sigil.Infrastructure.Persistence;

/// <summary>Resolves which projects the current user is allowed to see.</summary>
internal class ProjectAccessService(SigilDbContext dbContext, ICurrentUserService currentUserService) : IProjectAccessService
{
    private const string AdminRole = "Admin";

    public async Task<List<int>> GetVisibleProjectIdsAsync()
    {
        var user = await currentUserService.GetCurrentUserAsync();
        if (user is null) return [];

        if (user.Roles.Any(role => string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase)))
            return await dbContext.Projects.Select(p => p.Id).ToListAsync();

        var teamIds = await dbContext.TeamMemberships
            .Where(m => m.UserId == user.Id)
            .Select(m => m.TeamId)
            .ToListAsync();

        if (teamIds.Count == 0) return [];

        return await dbContext.Projects
            .Where(p => p.TeamId.HasValue && teamIds.Contains(p.TeamId.Value))
            .Select(p => p.Id)
            .ToListAsync();
    }
}
