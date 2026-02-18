using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;
using Sigil.Domain;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.infrastructure.Persistence;

internal class SetupService(
    SigilDbContext dbContext,
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IAppConfigService appConfigService,
    IDatabaseMigrator databaseMigrator) : ISetupService
{
    // Cached per process lifetime; null = unchecked, false = up to date, true = pending
    private static bool? _hasPendingMigrationsCache;
    
    public async Task<SetupStatus> GetSetupStatusAsync()
    {
        try
        {
            var value = await appConfigService.GetAsync(AppConfigKeys.SetupComplete);
            return new SetupStatus(IsComplete: value == "true");
        }
        catch
        {
            return new SetupStatus(IsComplete: false);
        }
    }

    public async Task<DbStatusResponse> GetDbStatusAsync()
    {
        try
        {
            var status = await databaseMigrator.CheckConnectionAsync();
            var pending = await databaseMigrator.GetPendingMigrationsAsync();
            var applied = await databaseMigrator.GetAppliedMigrationsAsync();
            
            if (status != DbConnectionStatus.Connected)
                return new DbStatusResponse(status, null, pending, applied);

            return new DbStatusResponse(DbConnectionStatus.Connected, null, pending, applied);
        }
        catch (Exception ex)
        {
            return new DbStatusResponse(DbConnectionStatus.ConnectionFailed, ex.Message, [], []);
        }
    }

    public async Task<bool> MigrateAsync()
    {
        var status = await GetSetupStatusAsync();
        if (status.IsComplete) return false;

        await databaseMigrator.MigrateAsync();
        return true;
    }

    public Task<DbStatusResponse> GetMaintenanceDbStatusAsync() => GetDbStatusAsync();

    public async Task<bool> HasPendingMigrationsAsync()
    {
        if (_hasPendingMigrationsCache.HasValue)
            return _hasPendingMigrationsCache.Value;

        var pending = await databaseMigrator.GetPendingMigrationsAsync();
        _hasPendingMigrationsCache = pending.Count > 0;
        return _hasPendingMigrationsCache.Value;
    }

    public async Task ApplyPendingMigrationsAsync()
    {
        await databaseMigrator.MigrateAsync();
        _hasPendingMigrationsCache = false;
    }

    public async Task<SetupResult> InitializeAsync(SetupRequest request)
    {
        // Guard against re-initialization
        var status = await GetSetupStatusAsync();
        if (status.IsComplete)
            return SetupResult.Failure("Setup has already been completed.");

        // Create admin user
        var admin = new User
        {
            UserName = request.AdminEmail,
            Email = request.AdminEmail,
            DisplayName = request.AdminDisplayName,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(admin, request.AdminPassword);
        if (!createResult.Succeeded)
            return SetupResult.Failure(createResult.Errors.Select(e => e.Description));

        // Ensure Admin role exists and assign it
        if (!await roleManager.RoleExistsAsync("Admin"))
            await roleManager.CreateAsync(new IdentityRole<Guid> { Name = "Admin" });
        await userManager.AddToRoleAsync(admin, "Admin");

        // Create team
        var team = new Team { Name = request.TeamName };
        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync();

        // Add admin to team
        dbContext.TeamMemberships.Add(new TeamMembership
        {
            UserId = admin.Id,
            User = admin,
            TeamId = team.Id,
            Team = team,
            Role = TeamRole.Owner
        });
        await dbContext.SaveChangesAsync();

        // Create project
        var project = new Project
        {
            Name = request.ProjectName,
            Platform = request.ProjectPlatform,
            ApiKey = RandomNumberGenerator.GetHexString(32).ToLower(),
            TeamId = team.Id
        };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        // Save host URL config if provided
        if (!string.IsNullOrWhiteSpace(request.HostUrl))
            await appConfigService.SetAsync(AppConfigKeys.HostUrl, request.HostUrl);

        await appConfigService.SetAsync(AppConfigKeys.SetupComplete, "true");

        // Sign in the admin
        await signInManager.SignInAsync(admin, isPersistent: false);

        var userInfo = new UserInfo(admin.Id, admin.Email, admin.DisplayName, admin.CreatedAt, admin.LastLogin, ["Admin"], IsActivated: true);
        return SetupResult.Success(userInfo, project.ApiKey, project.Id);
    }
}
