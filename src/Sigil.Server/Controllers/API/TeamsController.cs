using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Models.Teams;
using Sigil.Domain.Entities;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/teams")]
[Authorize]
public class TeamsController(UserManager<User> userManager) : SigilController
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        // Load user with team memberships
        var userWithTeams = await userManager.Users
            .Include(u => u.TeamMemberships)
                .ThenInclude(tm => tm.Team)
                    .ThenInclude(t => t.Members)
            .Include(u => u.TeamMemberships)
                .ThenInclude(tm => tm.Team)
                    .ThenInclude(t => t.Projects)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        if (userWithTeams is null)
            return Unauthorized();

        var teams = userWithTeams.TeamMemberships.Select(tm => new TeamResponse(
            tm.Team.Id,
            tm.Team.Name,
            tm.Team.Members.Count,
            tm.Team.Projects.Count));

        return Ok(teams);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var userWithTeam = await userManager.Users
            .Include(u => u.TeamMemberships)
                .ThenInclude(tm => tm.Team)
                    .ThenInclude(t => t.Members)
                        .ThenInclude(m => m.User)
            .Include(u => u.TeamMemberships)
                .ThenInclude(tm => tm.Team)
                    .ThenInclude(t => t.Projects)
            .FirstOrDefaultAsync(u => u.Id == user.Id);

        var membership = userWithTeam?.TeamMemberships.FirstOrDefault(tm => tm.TeamId == id);
        if (membership is null)
            return NotFound();

        var team = membership.Team;

        var members = team.Members.Select(m => new TeamMemberResponse(
            m.UserId, m.User.DisplayName, m.User.Email, m.Role)).ToList();

        var projects = team.Projects.Select(p => new TeamProjectResponse(
            p.Id, p.Name, p.Platform)).ToList();

        return Ok(new TeamDetailResponse(team.Id, team.Name, members, projects));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest request)
    {
        // Note: Team creation with DB access would ideally go through a service.
        // For MVP, we'll return a placeholder since we need a team service.
        // The setup wizard already creates the initial team.
        return BadRequest(new { errors = new[] { "Team creation via API is not yet supported. Use the setup wizard." } });
    }

    [HttpPost("{id:int}/members")]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddTeamMemberRequest request)
    {
        // Similar to above - needs a team service for proper implementation.
        return BadRequest(new { errors = new[] { "Member management via API is not yet supported." } });
    }
}
