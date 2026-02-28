using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Sigil.Application.Authorization;
using Sigil.Application.Interfaces;
using Sigil.Domain.Enums;

namespace Sigil.Server.Authorization;

public class SigilPermissionHandler(ITeamService teamService, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<SigilPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SigilPermissionRequirement requirement)
    {
        var permission = requirement.Permission;

        // Admins bypass all checks
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return;
        }

        // Admin-only permissions — non-admins always denied
        if (permission is SigilPermissions.CanAccessAdmin or SigilPermissions.CanInviteUsers)
            return;

        var userId = GetUserId(context.User);
        if (userId is null) return;

        // Project-scoped permissions
        if (permission is SigilPermissions.CanViewProject
            or SigilPermissions.CanEditIssue
            or SigilPermissions.CanDeleteIssue
            or SigilPermissions.CanManageProject
            or SigilPermissions.CanDeleteProject)
        {
            var projectId = GetRouteInt("projectId") ?? GetRouteInt("id");
            if (projectId is null) return;

            var role = await teamService.GetUserRoleForProjectAsync(userId.Value, projectId.Value);
            if (HasProjectPermission(role, permission))
                context.Succeed(requirement);
            return;
        }

        // Team-scoped permissions
        if (permission is SigilPermissions.CanViewTeam or SigilPermissions.CanManageTeam)
        {
            var teamId = GetRouteInt("id") ?? GetRouteInt("teamId");
            if (teamId is null) return;

            var role = await teamService.GetUserRoleForTeamAsync(userId.Value, teamId.Value);
            if (HasTeamPermission(role, permission))
                context.Succeed(requirement);
        }
    }

    private int? GetRouteInt(string key)
    {
        var val = httpContextAccessor.HttpContext?.GetRouteValue(key);
        if (val is null) return null;
        return int.TryParse(val.ToString(), out var i) ? i : null;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return claim is not null ? Guid.Parse(claim) : null;
    }

    private static bool HasProjectPermission(TeamRole? role, string permission) => permission switch
    {
        SigilPermissions.CanViewProject    => role is not null,
        SigilPermissions.CanEditIssue      => role is TeamRole.Member or TeamRole.Admin or TeamRole.Owner,
        SigilPermissions.CanDeleteIssue    => role is TeamRole.Admin or TeamRole.Owner,
        SigilPermissions.CanManageProject  => role is TeamRole.Admin or TeamRole.Owner,
        SigilPermissions.CanDeleteProject  => role is TeamRole.Owner,
        _                                  => false
    };

    private static bool HasTeamPermission(TeamRole? role, string permission) => permission switch
    {
        SigilPermissions.CanViewTeam   => role is not null,
        SigilPermissions.CanManageTeam => role is TeamRole.Admin or TeamRole.Owner,
        _                              => false
    };
}
