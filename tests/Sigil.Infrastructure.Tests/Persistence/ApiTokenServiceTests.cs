using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Infrastructure.Persistence;
using Sigil.Infrastructure.Tests.Fixtures;

namespace Sigil.Infrastructure.Tests.Persistence;

[Collection(DbCollection)]
public class ApiTokenServiceTests(TestDatabaseFixture fixture)
{
    private SigilDbContext Ctx() => TestHelper.CreateContext(fixture.ConnectionString);

    private static IDateTime StubDateTime(DateTime? dt = null)
    {
        var stub = Substitute.For<IDateTime>();
        stub.UtcNow.Returns(dt ?? new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        return stub;
    }

    private static ApiTokenService Create(SigilDbContext ctx, IDateTime? dateTime = null)
        => new(ctx, dateTime ?? StubDateTime());

    private static string HashOf(string token)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    // ── Create ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ReturnsPlaintextSecret()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        var created = await service.CreateAsync(user.Id, "CI token");

        created.Id.Should().BeGreaterThan(0);
        created.Name.Should().Be("CI token");
        created.Token.Should().StartWith("sigil_");
        created.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateAsync_PersistsOnlyTheHashOfTheToken()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        var created = await service.CreateAsync(user.Id, "CI token");

        await using var verify = Ctx();
        var stored = await verify.ApiTokens.FirstAsync(t => t.Id == created.Id);
        stored.TokenHash.Should().Be(HashOf(created.Token));
        stored.TokenHash.Should().NotBe(created.Token);
        stored.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task CreateAsync_StoresTheSecretOnlyInTheReturnedValue()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        var created = await service.CreateAsync(user.Id, "CI token");

        await using var verify = Ctx();
        var stored = await verify.ApiTokens.FirstAsync(t => t.Id == created.Id);
        string storedValues = string.Join("|", stored.Name, stored.TokenHash, stored.UserId.ToString());
        storedValues.Should().NotContain(created.Token);
    }

    // ── List ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ReturnsTokensWithoutHashOrSecret()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);
        var created = await service.CreateAsync(user.Id, "CI token");

        var tokens = await service.ListAsync(user.Id);

        tokens.Should().HaveCount(1);
        tokens[0].Id.Should().Be(created.Id);
        tokens[0].Name.Should().Be("CI token");
        tokens[0].CreatedAt.Should().Be(created.CreatedAt);
        tokens[0].RevokedAt.Should().BeNull();

        string rendered = string.Join("|", tokens[0].Id, tokens[0].Name, tokens[0].CreatedAt);
        rendered.Should().NotContain(created.Token);
        rendered.Should().NotContain(HashOf(created.Token));
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyTheCallersTokens()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var other = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);
        await service.CreateAsync(other.Id, "Other token");

        var tokens = await service.ListAsync(user.Id);

        tokens.Should().BeEmpty();
    }

    // ── Revoke ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAsync_MarksTheTokenRevoked()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);
        var created = await service.CreateAsync(user.Id, "CI token");

        bool revoked = await service.RevokeAsync(user.Id, created.Id);

        revoked.Should().BeTrue();
        await using var verify = Ctx();
        var stored = await verify.ApiTokens.FirstAsync(t => t.Id == created.Id);
        stored.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeAsync_UnknownToken_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);

        (await service.RevokeAsync(user.Id, 999999)).Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_TokenOfAnotherUser_ReturnsFalse()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var other = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);
        var created = await service.CreateAsync(other.Id, "Other token");

        (await service.RevokeAsync(user.Id, created.Id)).Should().BeFalse();
    }

    // ── Authenticate ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AuthenticateAsync_ValidToken_ReturnsTheOwner()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);
        var created = await service.CreateAsync(user.Id, "CI token");

        var identity = await service.AuthenticateAsync(created.Token);

        identity.Should().NotBeNull();
        identity!.UserId.Should().Be(user.Id);
        identity.Email.Should().Be(user.Email);
    }

    [Fact]
    public async Task AuthenticateAsync_ValidToken_CarriesTheOwnerRoles()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var roleName = $"Role-{Guid.NewGuid():N}";
        var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = roleName, NormalizedName = roleName.ToUpperInvariant() };
        ctx.Roles.Add(role);
        ctx.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();

        var service = Create(ctx);
        var created = await service.CreateAsync(user.Id, "CI token");

        var identity = await service.AuthenticateAsync(created.Token);

        identity.Should().NotBeNull();
        identity!.Roles.Should().Contain(roleName);
    }

    [Fact]
    public async Task AuthenticateAsync_ValidToken_RecordsLastUsedAt()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var service = Create(ctx, StubDateTime(now));
        var created = await service.CreateAsync(user.Id, "CI token");

        await service.AuthenticateAsync(created.Token);

        await using var verify = Ctx();
        var stored = await verify.ApiTokens.FirstAsync(t => t.Id == created.Id);
        stored.LastUsedAt.Should().Be(now);
    }

    [Fact]
    public async Task AuthenticateAsync_RevokedToken_IsRejected()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);
        var created = await service.CreateAsync(user.Id, "CI token");
        await service.RevokeAsync(user.Id, created.Id);

        (await service.AuthenticateAsync(created.Token)).Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_ExpiredToken_IsRejected()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);
        var created = await service.CreateAsync(user.Id, "CI token", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        (await service.AuthenticateAsync(created.Token)).Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_UnknownToken_IsRejected()
    {
        await using var ctx = Ctx();
        var service = Create(ctx);

        (await service.AuthenticateAsync("sigil_not-a-real-token")).Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_TokenWithoutStoredHashMatch_IsRejected()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);
        var created = await service.CreateAsync(user.Id, "CI token");

        (await service.AuthenticateAsync(created.Token + "x")).Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_TokenOfDeletedUser_IsRejected()
    {
        await using var ctx = Ctx();
        var user = await TestHelper.CreateUserAsync(ctx);
        var service = Create(ctx);
        var created = await service.CreateAsync(user.Id, "CI token");

        ctx.Users.Remove(user);
        await ctx.SaveChangesAsync();

        (await service.AuthenticateAsync(created.Token)).Should().BeNull();
    }
}
