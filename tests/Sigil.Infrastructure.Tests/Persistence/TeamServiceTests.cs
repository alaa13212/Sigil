using Microsoft.EntityFrameworkCore;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class TeamServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static string UniqueName(string prefix = "Team") => $"{prefix}-{Guid.NewGuid():N}";

    [Fact]
    public async Task CreateTeam_PersistsWithOwnerMembership()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = new TeamService(ctx);
        var name = UniqueName("MyTeam");

        var result = await service.CreateTeamAsync(name, user.Id);

        result.Id.Should().BeGreaterThan(0);
        result.Name.Should().Be(name);
        result.MemberCount.Should().Be(1);

        // Verify membership in DB
        await using var verifyCtx = Ctx();
        var membership = await verifyCtx.TeamMemberships
            .FirstOrDefaultAsync(m => m.TeamId == result.Id && m.UserId == user.Id);
        membership.Should().NotBeNull();
        membership.Role.Should().Be(TeamRole.Owner);
    }

    [Fact]
    public async Task GetTeams_ReturnsOrderedByName()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = new TeamService(ctx);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        await service.CreateTeamAsync($"Z-Team-{suffix}", user.Id);
        await service.CreateTeamAsync($"A-Team-{suffix}", user.Id);

        var teams = await service.GetTeamsAsync();
        var relevant = teams.Where(t => t.Name.EndsWith(suffix)).Select(t => t.Name).ToList();

        relevant.Should().HaveCount(2);
        relevant.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task UpdateTeam_ChangesName()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = new TeamService(ctx);
        var created = await service.CreateTeamAsync(UniqueName("Before"), user.Id);
        var newName = UniqueName("After");

        var updated = await service.UpdateTeamAsync(created.Id, newName);

        updated.Should().NotBeNull();
        updated.Name.Should().Be(newName);
    }

    [Fact]
    public async Task UpdateTeam_NonExistent_ReturnsNull()
    {
        await using var ctx = Ctx();
        var service = new TeamService(ctx);

        (await service.UpdateTeamAsync(999999, UniqueName())).Should().BeNull();
    }

    [Fact]
    public async Task DeleteTeam_RemovesFromDb()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = new TeamService(ctx);
        var created = await service.CreateTeamAsync(UniqueName("ToDelete"), user.Id);

        (await service.DeleteTeamAsync(created.Id)).Should().BeTrue();

        await using var verifyCtx = Ctx();
        var inDb = await verifyCtx.Teams.FindAsync(created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task DeleteTeam_NonExistent_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var service = new TeamService(ctx);

        (await service.DeleteTeamAsync(999999)).Should().BeFalse();
    }

    [Fact]
    public async Task AddMember_AddsNewMembership()
    {
        await using var ctx = Ctx();
        var owner = await TestHelper.CreateUserAsync(ctx, "Owner");
        var member = await TestHelper.CreateUserAsync(ctx, "Member");
        var service = new TeamService(ctx);
        var team = await service.CreateTeamAsync(UniqueName(), owner.Id);

        var result = await service.AddMemberAsync(team.Id, member.Id, TeamRole.Member);

        result.Should().BeTrue();

        await using var verifyCtx = Ctx();
        var membership = await verifyCtx.TeamMemberships
            .FirstOrDefaultAsync(m => m.TeamId == team.Id && m.UserId == member.Id);
        membership.Should().NotBeNull();
        membership.Role.Should().Be(TeamRole.Member);
    }

    [Fact]
    public async Task AddMember_DuplicateUser_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = new TeamService(ctx);
        var team = await service.CreateTeamAsync(UniqueName(), user.Id);

        // user is already owner
        (await service.AddMemberAsync(team.Id, user.Id, TeamRole.Member)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMember_RemovesMembership()
    {
        await using var ctx = Ctx();
        var owner = await TestHelper.CreateUserAsync(ctx, "Owner");
        var member = await TestHelper.CreateUserAsync(ctx, "Member");
        var service = new TeamService(ctx);
        var team = await service.CreateTeamAsync(UniqueName(), owner.Id);
        await service.AddMemberAsync(team.Id, member.Id, TeamRole.Member);

        (await service.RemoveMemberAsync(team.Id, member.Id)).Should().BeTrue();

        await using var verifyCtx = Ctx();
        var gone = await verifyCtx.TeamMemberships
            .AnyAsync(m => m.TeamId == team.Id && m.UserId == member.Id);
        gone.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateMemberRole_ChangesRole()
    {
        await using var ctx = Ctx();
        var owner = await TestHelper.CreateUserAsync(ctx, "Owner");
        var member = await TestHelper.CreateUserAsync(ctx, "Member");
        var service = new TeamService(ctx);
        var team = await service.CreateTeamAsync(UniqueName(), owner.Id);
        await service.AddMemberAsync(team.Id, member.Id, TeamRole.Member);

        (await service.UpdateMemberRoleAsync(team.Id, member.Id, TeamRole.Admin)).Should().BeTrue();

        await using var verifyCtx = Ctx();
        var membership = await verifyCtx.TeamMemberships
            .FirstAsync(m => m.TeamId == team.Id && m.UserId == member.Id);
        membership.Role.Should().Be(TeamRole.Admin);
    }

    [Fact]
    public async Task GetUserRoleForTeam_ReturnsMemberRole()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = new TeamService(ctx);
        var team = await service.CreateTeamAsync(UniqueName(), user.Id);

        var role = await service.GetUserRoleForTeamAsync(user.Id, team.Id);

        role.Should().Be(TeamRole.Owner);
    }

    [Fact]
    public async Task GetUserRoleForTeam_NonMember_ReturnsNull()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var other = await TestHelper.CreateUserAsync(ctx);
        var service = new TeamService(ctx);
        var team = await service.CreateTeamAsync(UniqueName(), user.Id);

        var role = await service.GetUserRoleForTeamAsync(other.Id, team.Id);

        role.Should().BeNull();
    }

    [Fact]
    public async Task GetUserRoleForProject_ProjectWithTeam_ReturnsRole()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = new TeamService(ctx);
        var team = await service.CreateTeamAsync(UniqueName(), user.Id);

        // Assign a project to the team
        var project = await TestHelper.CreateProjectAsync(ctx);
        var tracked = await ctx.Projects.AsTracking().FirstAsync(p => p.Id == project.Id);
        tracked.TeamId = team.Id;
        await ctx.SaveChangesAsync();

        var role = await service.GetUserRoleForProjectAsync(user.Id, project.Id);

        role.Should().Be(TeamRole.Owner);
    }

    [Fact]
    public async Task GetUserRoleForProject_ProjectWithoutTeam_ReturnsNull()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = new TeamService(ctx);
        var project = await TestHelper.CreateProjectAsync(ctx);

        var role = await service.GetUserRoleForProjectAsync(user.Id, project.Id);

        role.Should().BeNull();
    }
}
