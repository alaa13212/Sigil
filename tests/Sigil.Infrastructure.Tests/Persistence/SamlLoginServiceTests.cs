using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Models.Saml;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Services;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class SamlLoginServiceTests(TestDatabaseFixture fixture)
{
    private IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SigilDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        services.AddIdentity<User, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<SigilDbContext>()
            .AddDefaultTokenProviders();
        return services.BuildServiceProvider();
    }

    private async Task<SamlConnection> CreateConnectionAsync(
        List<SamlGroupMappingRequest>? mappings = null,
        string? defaultTeam = null)
    {
        await using var scope = BuildProvider().CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();

        var connection = new SamlConnection
        {
            Id = Guid.NewGuid(),
            Name = $"saml_{Guid.NewGuid():N}",
            EntityId = $"https://idp.example.com/{Guid.NewGuid():N}",
            IsEnabled = true,
            EmailClaimType = "email",
            NameClaimType = "name",
            GroupsClaimType = "groups",
            DefaultTeamName = defaultTeam,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        foreach (var mapping in mappings ?? [])
        {
            connection.GroupMappings.Add(new SamlGroupMapping
            {
                Id = Guid.NewGuid(),
                GroupName = mapping.GroupName,
                TeamName = mapping.TeamName,
                Role = mapping.Role,
                GrantsGlobalAdmin = mapping.GrantsGlobalAdmin
            });
        }

        ctx.SamlConnections.Add(connection);
        await ctx.SaveChangesAsync();

        return connection;
    }

    private static SamlAssertion Assertion(string email, params string[] groups) =>
        new(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["email"] = [email],
            ["name"] = ["Test User"],
            ["groups"] = groups
        });

    private async Task<SamlLoginResult> CompleteLoginAsync(SamlConnection connection, SamlAssertion assertion)
    {
        await using var scope = BuildProvider().CreateAsyncScope();
        var service = new SamlLoginService(
            scope.ServiceProvider.GetRequiredService<SigilDbContext>(),
            scope.ServiceProvider.GetRequiredService<UserManager<User>>(),
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
            new DateTimeProvider());

        return await service.CompleteLoginAsync(connection.Id, assertion);
    }

    private async Task<User?> FindUserAsync(string email)
    {
        await using var scope = BuildProvider().CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        return await userManager.FindByEmailAsync(email);
    }

    [Fact]
    public async Task CompleteLoginAsync_UnknownEmail_CreatesPendingUser()
    {
        var connection = await CreateConnectionAsync();
        var email = $"new_{Guid.NewGuid():N}@example.com";

        var result = await CompleteLoginAsync(connection, Assertion(email));

        result.Status.Should().Be(SamlLoginStatus.Pending);
        result.UserId.Should().NotBeNull();

        var user = await FindUserAsync(email);
        user.Should().NotBeNull();
        user!.SamlStatus.Should().Be(SamlUserStatus.Pending);
        user.SamlConnectionId.Should().Be(connection.Id);
        user.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteLoginAsync_MissingEmailClaim_Fails()
    {
        var connection = await CreateConnectionAsync();

        var result = await CompleteLoginAsync(connection, new SamlAssertion(
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)));

        result.Status.Should().Be(SamlLoginStatus.Failed);
        result.UserId.Should().BeNull();
    }

    [Fact]
    public async Task CompleteLoginAsync_ApprovedUser_AppliesGroupMappings()
    {
        var connection = await CreateConnectionAsync(
        [
            new SamlGroupMappingRequest("engineering", "Engineering", TeamRole.Admin, true)
        ]);

        var email = $"mapped_{Guid.NewGuid():N}@example.com";

        var pending = await CompleteLoginAsync(connection, Assertion(email, "engineering"));
        pending.Status.Should().Be(SamlLoginStatus.Pending);

        await using (var scope = BuildProvider().CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
            var user = await ctx.Users.AsTracking().FirstAsync(u => u.Id == pending.UserId);
            user.SamlStatus = SamlUserStatus.Approved;
            await ctx.SaveChangesAsync();
        }

        var approved = await CompleteLoginAsync(connection, Assertion(email, "engineering"));
        approved.Status.Should().Be(SamlLoginStatus.Approved);

        await using (var scope = BuildProvider().CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();

            var team = await ctx.Teams.FirstAsync(t => t.Name == "Engineering");
            var membership = await ctx.TeamMemberships
                .FirstAsync(m => m.UserId == pending.UserId && m.TeamId == team.Id);

            membership.Role.Should().Be(TeamRole.Admin);

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByIdAsync(pending.UserId!.Value.ToString());
            (await userManager.IsInRoleAsync(user!, "Admin")).Should().BeTrue();
        }
    }

    [Fact]
    public async Task CompleteLoginAsync_UnmatchedGroup_UsesDefaultTeam()
    {
        var connection = await CreateConnectionAsync(defaultTeam: "Fallback");
        var email = $"default_{Guid.NewGuid():N}@example.com";

        var pending = await CompleteLoginAsync(connection, Assertion(email, "something-else"));
        pending.Status.Should().Be(SamlLoginStatus.Pending);

        await using (var scope = BuildProvider().CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
            var user = await ctx.Users.AsTracking().FirstAsync(u => u.Id == pending.UserId);
            user.SamlStatus = SamlUserStatus.Approved;
            await ctx.SaveChangesAsync();
        }

        var approved = await CompleteLoginAsync(connection, Assertion(email, "something-else"));
        approved.Status.Should().Be(SamlLoginStatus.Approved);

        await using (var scope = BuildProvider().CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();

            var team = await ctx.Teams.FirstAsync(t => t.Name == "Fallback");
            var membership = await ctx.TeamMemberships
                .FirstAsync(m => m.UserId == pending.UserId && m.TeamId == team.Id);

            membership.Role.Should().Be(TeamRole.Member);
        }
    }

    [Fact]
    public async Task CompleteLoginAsync_RejectedUser_IsDenied()
    {
        var connection = await CreateConnectionAsync();
        var email = $"rejected_{Guid.NewGuid():N}@example.com";

        var pending = await CompleteLoginAsync(connection, Assertion(email));
        pending.Status.Should().Be(SamlLoginStatus.Pending);

        await using (var scope = BuildProvider().CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
            var user = await ctx.Users.AsTracking().FirstAsync(u => u.Id == pending.UserId);
            user.SamlStatus = SamlUserStatus.Rejected;
            await ctx.SaveChangesAsync();
        }

        var result = await CompleteLoginAsync(connection, Assertion(email));
        result.Status.Should().Be(SamlLoginStatus.Rejected);
    }

    [Fact]
    public async Task CompleteLoginAsync_DisabledConnection_Fails()
    {
        var connection = await CreateConnectionAsync();

        await using (var scope = BuildProvider().CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<SigilDbContext>();
            var stored = await ctx.SamlConnections.AsTracking().FirstAsync(c => c.Id == connection.Id);
            stored.IsEnabled = false;
            await ctx.SaveChangesAsync();
        }

        var result = await CompleteLoginAsync(connection, Assertion($"disabled_{Guid.NewGuid():N}@example.com"));
        result.Status.Should().Be(SamlLoginStatus.Failed);
    }
}
