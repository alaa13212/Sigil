using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Teams;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
[Route("api/teams")]
[Authorize]
public class TeamsController(ITeamService teamService, UserManager<User> userManager) : SigilController
{
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var teams = await teamService.GetTeamsAsync();
        return Ok(teams);
    }

    [Authorize(Policy = SigilPermissions.CanViewTeam)]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var detail = await teamService.GetTeamDetailAsync(id);
        return detail is not null ? Ok(detail) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest request)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var team = await teamService.CreateTeamAsync(request.Name, user.Id);
        return CreatedAtAction(nameof(Get), new { id = team.Id }, team);
    }

    [Authorize(Policy = SigilPermissions.CanManageTeam)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateTeamRequest request)
    {
        var result = await teamService.UpdateTeamAsync(id, request.Name);
        return result is not null ? Ok(result) : NotFound();
    }

    [Authorize(Policy = SigilPermissions.CanManageTeam)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await teamService.DeleteTeamAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [Authorize(Policy = SigilPermissions.CanManageTeam)]
    [HttpPost("{id:int}/members")]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddTeamMemberRequest request)
    {
        var added = await teamService.AddMemberAsync(id, request.UserId, request.Role);
        return added ? Ok() : BadRequest(new { errors = new[] { "Could not add member. User may already be a member." } });
    }

    [Authorize(Policy = SigilPermissions.CanManageTeam)]
    [HttpDelete("{teamId:int}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(int teamId, Guid userId)
    {
        var removed = await teamService.RemoveMemberAsync(teamId, userId);
        return removed ? NoContent() : NotFound();
    }

    [Authorize(Policy = SigilPermissions.CanManageTeam)]
    [HttpPut("{teamId:int}/members/{userId:guid}")]
    public async Task<IActionResult> UpdateMemberRole(int teamId, Guid userId, [FromBody] UpdateMemberRoleRequest request)
    {
        var updated = await teamService.UpdateMemberRoleAsync(teamId, userId, request.Role);
        return updated ? Ok() : NotFound();
    }
}

public record UpdateMemberRoleRequest(TeamRole Role);
