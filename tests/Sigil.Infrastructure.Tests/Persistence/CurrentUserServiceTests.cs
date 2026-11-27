using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigil.Domain.Entities;
using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Tests.Persistence;

public class CurrentUserServiceTests
{
    [Fact]
    public async Task GetCurrentUserAsync_NoHttpContext_ReturnsNull()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var service = new CurrentUserService(CreateUserManager(), accessor);

        var result = await service.GetCurrentUserAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentUserAsync_AnonymousPrincipal_ReturnsNull()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) });

        var service = new CurrentUserService(CreateUserManager(), accessor);

        var result = await service.GetCurrentUserAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentUserAsync_UnknownUser_ReturnsNull()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext
        {
            User = PrincipalFor(Guid.NewGuid())
        });

        var service = new CurrentUserService(CreateUserManager(), accessor);

        var result = await service.GetCurrentUserAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentUserAsync_AuthenticatedUser_ReturnsUserWithRoles()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "dev@sigil.test",
            DisplayName = "Dev",
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            LastLogin = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            EmailConfirmed = true
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext { User = PrincipalFor(user.Id) });

        var service = new CurrentUserService(CreateUserManager(user, ["Admin"]), accessor);

        var result = await service.GetCurrentUserAsync();

        result.Should().NotBeNull();
        result!.Id.Should().Be(user.Id);
        result.Email.Should().Be(user.Email);
        result.DisplayName.Should().Be(user.DisplayName);
        result.CreatedAt.Should().Be(user.CreatedAt);
        result.LastLogin.Should().Be(user.LastLogin);
        result.IsActivated.Should().BeTrue();
        result.Roles.Should().Contain("Admin");
    }

    private static ClaimsPrincipal PrincipalFor(Guid userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "cookie"));

    private static UserManager<User> CreateUserManager(User? user = null, IList<string>? roles = null)
    {
        var store = Substitute.For<IUserStore<User>>();
        if (user is not null)
            store.FindByIdAsync(user.Id.ToString(), Arg.Any<CancellationToken>()).Returns(user);

        var manager = Substitute.For<UserManager<User>>(
            store,
            Options.Create(new IdentityOptions()),
            Substitute.For<IPasswordHasher<User>>(),
            Array.Empty<IUserValidator<User>>(),
            Array.Empty<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<User>>>());

        if (user is not null)
        {
            manager.GetUserAsync(Arg.Any<ClaimsPrincipal>()).Returns(user);
            manager.GetRolesAsync(user).Returns(roles ?? []);
        }

        return manager;
    }
}
