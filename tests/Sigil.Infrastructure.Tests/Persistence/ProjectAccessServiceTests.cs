using NSubstitute;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class ProjectAccessServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static ICurrentUserService StubCurrentUser(Guid? userId, params string[] roles)
    {
        var current = Substitute.For<ICurrentUserService>();
        var info = userId is null
            ? null
            : new UserInfo(userId.Value, "dev@test.com", "Dev", DateTime.UtcNow, null, roles);
        current.GetCurrentUserAsync().Returns(Task.FromResult<UserInfo?>(info));
        return current;
    }

    private static async Task<Project> CreateProjectAsync(SigilDbContext ctx, int? teamId)
    {
        var project = new Project
        {
            Name = $"Access-{Guid.NewGuid():N}",
            Platform = Platform.CSharp,
            ApiKey = Guid.NewGuid().ToString("N"),
            TeamId = teamId,
        };
        ctx.Projects.Add(project);
        await ctx.SaveChangesAsync();
        return project;
    }

    private static async Task<(Team Team, TeamMembership Membership)> CreateTeamAsync(SigilDbContext ctx, User member)
    {
        var team = new Team { Name = $"Team-{Guid.NewGuid():N}" };
        ctx.Teams.Add(team);
        await ctx.SaveChangesAsync();

        var membership = new TeamMembership
        {
            UserId = member.Id,
            TeamId = team.Id,
            Role = TeamRole.Member,
        };
        ctx.TeamMemberships.Add(membership);
        await ctx.SaveChangesAsync();
        return (team, membership);
    }

    [Fact]
    public async Task VisibleProjects_ForAnAdmin_AreEveryProjectOfTheInstance()
    {
        await using var ctx = Ctx();
        var team = await CreateTeamAsync(ctx, await TestHelper.CreateUserAsync(ctx));
        var inTeam = await CreateProjectAsync(ctx, team.Team.Id);
        var withoutTeam = await CreateProjectAsync(ctx, null);
        var service = new ProjectAccessService(ctx, StubCurrentUser(Guid.NewGuid(), "Admin"));

        var visible = await service.GetVisibleProjectIdsAsync();

        visible.Should().Contain(inTeam.Id);
        visible.Should().Contain(withoutTeam.Id);
    }

    [Fact]
    public async Task VisibleProjects_ForATeamMember_AreTheProjectsOfTheirTeam()
    {
        await using var ctx = Ctx();
        var member = await TestHelper.CreateUserAsync(ctx);
        var (team, _) = await CreateTeamAsync(ctx, member);
        var (otherTeam, _) = await CreateTeamAsync(ctx, await TestHelper.CreateUserAsync(ctx));
        var mine = await CreateProjectAsync(ctx, team.Id);
        var theirs = await CreateProjectAsync(ctx, otherTeam.Id);
        var service = new ProjectAccessService(ctx, StubCurrentUser(member.Id));

        var visible = await service.GetVisibleProjectIdsAsync();

        visible.Should().Contain(mine.Id);
        visible.Should().NotContain(theirs.Id);
    }

    [Fact]
    public async Task VisibleProjects_ForATeamMember_ExcludeProjectsWithoutATeam()
    {
        await using var ctx = Ctx();
        var member = await TestHelper.CreateUserAsync(ctx);
        var (team, _) = await CreateTeamAsync(ctx, member);
        var orphan = await CreateProjectAsync(ctx, null);
        var service = new ProjectAccessService(ctx, StubCurrentUser(member.Id));

        var visible = await service.GetVisibleProjectIdsAsync();

        visible.Should().NotContain(orphan.Id);
    }

    [Fact]
    public async Task VisibleProjects_ForAMemberOfNoTeam_AreEmpty()
    {
        await using var ctx = Ctx();
        var member = await TestHelper.CreateUserAsync(ctx);
        await CreateProjectAsync(ctx, null);
        var service = new ProjectAccessService(ctx, StubCurrentUser(member.Id));

        var visible = await service.GetVisibleProjectIdsAsync();

        visible.Should().BeEmpty();
    }

    [Fact]
    public async Task VisibleProjects_WithoutAUser_AreEmpty()
    {
        await using var ctx = Ctx();
        var (team, _) = await CreateTeamAsync(ctx, await TestHelper.CreateUserAsync(ctx));
        await CreateProjectAsync(ctx, team.Id);
        var service = new ProjectAccessService(ctx, StubCurrentUser(null));

        var visible = await service.GetVisibleProjectIdsAsync();

        visible.Should().BeEmpty();
    }
}
