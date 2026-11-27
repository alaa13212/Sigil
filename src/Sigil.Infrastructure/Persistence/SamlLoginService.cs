using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Saml;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Interfaces;

namespace Sigil.Infrastructure.Persistence;

internal class SamlLoginService(
    SigilDbContext dbContext,
    UserManager<User> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IDateTime dateTime) : ISamlLoginService
{
    private const string AdminRoleName = "Admin";

    private static readonly string[] EmailClaimTypes =
    [
        "email",
        "mail",
        "emailAddress",
        System.Security.Claims.ClaimTypes.Email
    ];

    private static readonly string[] NameClaimTypes =
    [
        "name",
        "displayName",
        "given_name",
        System.Security.Claims.ClaimTypes.Name
    ];

    private static readonly string[] GroupClaimTypes =
    [
        "groups",
        "roles",
        "group",
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/roles"
    ];

    public async Task<SamlLoginResult> CompleteLoginAsync(Guid connectionId, SamlAssertion assertion)
    {
        ArgumentNullException.ThrowIfNull(assertion);

        var connection = await dbContext.SamlConnections
            .AsTracking()
            .Include(c => c.GroupMappings)
            .FirstOrDefaultAsync(c => c.Id == connectionId);

        if (connection is null || !connection.IsEnabled)
            return new SamlLoginResult(SamlLoginStatus.Failed, null, "This sign-in connection is not available.");

        var email = GetClaim(assertion, connection.EmailClaimType, EmailClaimTypes);
        if (email is null)
            return new SamlLoginResult(SamlLoginStatus.Failed, null, "The identity provider did not return an email address.");

        var nameId = GetFirstClaimValue(assertion, System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? GetFirstClaimValue(assertion, "nameId");

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            var created = await CreateUserAsync(email, GetClaim(assertion, connection.NameClaimType, NameClaimTypes), nameId, connectionId);
            if (created is null)
                return new SamlLoginResult(SamlLoginStatus.Failed, null, "The account could not be created.");

            return new SamlLoginResult(SamlLoginStatus.Pending, created.Id, "Your account is waiting for administrator approval.");
        }

        if (user.SamlStatus == SamlUserStatus.Rejected)
            return new SamlLoginResult(SamlLoginStatus.Rejected, user.Id, "Access to Sigil has been denied for this account.");

        if (user.SamlStatus != SamlUserStatus.Approved)
        {
            await MarkPendingAsync(user.Id, connectionId, nameId);
            return new SamlLoginResult(SamlLoginStatus.Pending, user.Id, "Your account is waiting for administrator approval.");
        }

        await ApplyAccessAsync(connection, user.Id, GetGroups(assertion, connection));

        return new SamlLoginResult(SamlLoginStatus.Approved, user.Id, null);
    }

    public async Task ReconcileConnectionAsync(Guid connectionId)
    {
        var connection = await dbContext.SamlConnections
            .AsTracking()
            .Include(c => c.GroupMappings)
            .FirstOrDefaultAsync(c => c.Id == connectionId);

        if (connection is null) return;

        var users = await dbContext.Users
            .Where(u => u.SamlConnectionId == connectionId && u.SamlStatus == SamlUserStatus.Approved)
            .ToListAsync();

        foreach (var user in users)
            await ApplyAccessAsync(connection, user.Id, ParseGroups(user.SamlGroups));
    }

    public async Task RevokeConnectionAsync(Guid connectionId)
    {
        var users = await dbContext.Users
            .Where(u => u.SamlAdminConnectionId == connectionId)
            .ToListAsync();

        foreach (var user in users)
        {
            var tracked = await dbContext.Users.AsTracking().FirstAsync(u => u.Id == user.Id);
            var identity = await userManager.FindByIdAsync(user.Id.ToString());

            if (identity is not null)
                await userManager.RemoveFromRoleAsync(identity, AdminRoleName);

            tracked.SamlAdminConnectionId = null;
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<User?> CreateUserAsync(string email, string? displayName, string? nameId, Guid connectionId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            CreatedAt = dateTime.UtcNow,
            SamlStatus = SamlUserStatus.Pending,
            SamlConnectionId = connectionId,
            SamlNameId = nameId
        };

        var result = await userManager.CreateAsync(user);
        return result.Succeeded ? user : null;
    }

    private async Task MarkPendingAsync(Guid userId, Guid connectionId, string? nameId)
    {
        var user = await dbContext.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return;

        user.SamlStatus = SamlUserStatus.Pending;
        user.SamlConnectionId = connectionId;
        user.SamlNameId = nameId;
        await dbContext.SaveChangesAsync();
    }

    private async Task ApplyAccessAsync(SamlConnection connection, Guid userId, IReadOnlyCollection<string> groups)
    {
        var matched = connection.GroupMappings
            .Where(m => groups.Contains(m.GroupName, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var desired = new Dictionary<int, TeamRole>();

        foreach (var mapping in matched)
        {
            if (string.IsNullOrWhiteSpace(mapping.TeamName)) continue;

            var team = await GetOrCreateTeamAsync(mapping.TeamName);
            desired[team.Id] = desired.TryGetValue(team.Id, out var current) && current > mapping.Role
                ? current
                : mapping.Role;
        }

        if (matched.Count == 0 && !string.IsNullOrWhiteSpace(connection.DefaultTeamName))
        {
            var team = await GetOrCreateTeamAsync(connection.DefaultTeamName);
            desired[team.Id] = desired.TryGetValue(team.Id, out var current) && current > TeamRole.Member
                ? current
                : TeamRole.Member;
        }

        var memberships = await dbContext.TeamMemberships
            .AsTracking()
            .Where(m => m.UserId == userId)
            .ToListAsync();

        foreach (var membership in memberships.Where(m => m.SamlConnectionId == connection.Id))
        {
            if (desired.TryGetValue(membership.TeamId, out var role))
                membership.Role = role;
            else
                dbContext.TeamMemberships.Remove(membership);
        }

        foreach (var (teamId, role) in desired)
        {
            if (memberships.Any(m => m.TeamId == teamId)) continue;

            dbContext.TeamMemberships.Add(new TeamMembership
            {
                UserId = userId,
                TeamId = teamId,
                Role = role,
                SamlConnectionId = connection.Id
            });
        }

        var tracked = await dbContext.Users.AsTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (tracked is not null)
            tracked.SamlGroups = JsonSerializer.Serialize(groups);

        await dbContext.SaveChangesAsync();

        await SyncAdminRoleAsync(connection, userId, matched.Any(m => m.GrantsGlobalAdmin));
    }

    private async Task SyncAdminRoleAsync(SamlConnection connection, Guid userId, bool grantsAdmin)
    {
        var tracked = await dbContext.Users.AsTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (tracked is null) return;

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return;

        if (grantsAdmin)
        {
            if (!await roleManager.RoleExistsAsync(AdminRoleName))
                await roleManager.CreateAsync(new IdentityRole<Guid> { Name = AdminRoleName });

            if (!await userManager.IsInRoleAsync(user, AdminRoleName))
            {
                await userManager.AddToRoleAsync(user, AdminRoleName);
                tracked.SamlAdminConnectionId = connection.Id;
            }
        }
        else if (tracked.SamlAdminConnectionId == connection.Id)
        {
            await userManager.RemoveFromRoleAsync(user, AdminRoleName);
            tracked.SamlAdminConnectionId = null;
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<Team> GetOrCreateTeamAsync(string name)
    {
        var trimmed = name.Trim();

        var team = await dbContext.Teams
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Name == trimmed);

        if (team is not null) return team;

        team = new Team { Name = trimmed };
        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync();

        return team;
    }

    private static IReadOnlyCollection<string> ParseGroups(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return [];

        try
        {
            return JsonSerializer.Deserialize<string[]>(stored) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? GetClaim(SamlAssertion assertion, string? configuredType, IReadOnlyCollection<string> fallbacks)
    {
        if (!string.IsNullOrWhiteSpace(configuredType))
        {
            if (TryGetValue(assertion, configuredType, out var configured) && !string.IsNullOrWhiteSpace(configured))
                return configured;
        }

        foreach (var fallback in fallbacks)
        {
            if (TryGetValue(assertion, fallback, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? GetFirstClaimValue(SamlAssertion assertion, string claimType)
    {
        if (!assertion.Claims.TryGetValue(claimType, out var values) || values is null) return null;

        var first = values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return first?.Trim();
    }

    private static bool TryGetValue(SamlAssertion assertion, string claimType, out string value)
    {
        value = string.Empty;

        if (!assertion.Claims.TryGetValue(claimType, out var values) || values is null)
            return false;

        var first = values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (first is null) return false;

        value = first.Trim();
        return true;
    }

    private static IReadOnlyCollection<string> GetGroups(SamlAssertion assertion, SamlConnection connection)
    {
        var groups = new List<string>();

        foreach (var claimType in new[] { connection.GroupsClaimType }.Concat(GroupClaimTypes))
        {
            if (string.IsNullOrWhiteSpace(claimType)) continue;
            if (!assertion.Claims.TryGetValue(claimType, out var values) || values is null) continue;

            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) continue;

                foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!groups.Contains(part, StringComparer.OrdinalIgnoreCase))
                        groups.Add(part);
                }
            }
        }

        return groups;
    }
}
