using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.ApiTokens;
using Sigil.Server.Auth;

namespace Sigil.Server.Tests.ApiToken;

/// <summary>Exercises the authentication dispatch that Program.cs wires up: API tokens for read-only API calls, cookie elsewhere.</summary>
public class ApiTokenAuthenticationTests
{
    private const string ValidToken = "sigil_valid_token";
    private const string RevokedToken = "sigil_revoked_token";
    private const string ExpiredToken = "sigil_expired_token";

    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ── Read-only API calls ────────────────────────────────────────────────────

    [Fact]
    public async Task GetApiRequest_WithValidBearerToken_IsAuthenticatedAsTheOwner()
    {
        var harness = CreateHarness();
        var context = harness.CreateContext("GET", "/api/projects", ValidToken);

        var result = await context.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        ClaimsPrincipal principal = result.Principal!;
        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(OwnerId.ToString());
        principal.FindFirstValue(ClaimTypes.Name).Should().Be("owner@sigil.test");
    }

    [Fact]
    public async Task GetApiRequest_WithValidBearerToken_CarriesTheOwnerRoles()
    {
        var harness = CreateHarness();
        harness.TokenService.AddRole(OwnerId, "Admin");
        var context = harness.CreateContext("GET", "/api/projects", ValidToken);

        var result = await context.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.Principal!.IsInRole("Admin").Should().BeTrue();
    }

    [Fact]
    public async Task GetApiRequest_WithRevokedToken_IsRejectedWith401()
    {
        var harness = CreateHarness();
        var context = harness.CreateContext("GET", "/api/projects", RevokedToken);

        var result = await context.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        await context.ChallengeAsync();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetApiRequest_WithExpiredToken_IsRejectedWith401()
    {
        var harness = CreateHarness();
        var context = harness.CreateContext("GET", "/api/projects", ExpiredToken);

        var result = await context.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        await context.ChallengeAsync();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task GetApiRequest_WithUnknownToken_IsRejectedWith401()
    {
        var harness = CreateHarness();
        var context = harness.CreateContext("GET", "/api/projects", "sigil_unknown_token");

        var result = await context.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        await context.ChallengeAsync();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    // ── Read-only guarantee ────────────────────────────────────────────────────

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task WriteRequest_WithBearerToken_IsNotAuthenticatedByTheToken(string method)
    {
        var harness = CreateHarness();
        var context = harness.CreateContext(method, "/api/projects/1/issues", ValidToken);

        var result = await context.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        context.User.FindFirstValue(ClaimTypes.NameIdentifier).Should().BeNull();
    }

    [Fact]
    public async Task WriteRequest_WithBearerTokenOnly_IsRejectedWith401()
    {
        var harness = CreateHarness();
        var context = harness.CreateContext("POST", "/api/projects/1/issues", ValidToken);

        await context.AuthenticateAsync();
        await context.ChallengeAsync();

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task WriteRequest_WithCookieSession_IsStillAuthenticated()
    {
        var harness = CreateHarness();
        string cookie = await harness.CreateSessionCookie(OwnerId);
        var context = harness.CreateContext("POST", "/api/projects/1/issues", cookie: cookie);

        var result = await context.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        ClaimsPrincipal principal = result.Principal!;
        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(OwnerId.ToString());
    }

    // ── Cookie flow ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApiRequest_WithCookieSession_IsAuthenticated()
    {
        var harness = CreateHarness();
        string cookie = await harness.CreateSessionCookie(OwnerId);
        var context = harness.CreateContext("GET", "/api/projects", cookie: cookie);

        var result = await context.AuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        ClaimsPrincipal principal = result.Principal!;
        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be(OwnerId.ToString());
    }

    [Fact]
    public async Task NonApiRequest_WithBearerToken_IsNotAuthenticatedByTheToken()
    {
        var harness = CreateHarness();
        var context = harness.CreateContext("GET", "/issues", ValidToken);

        var result = await context.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ApiRequest_WithoutCredentials_IsRejectedWith401()
    {
        var harness = CreateHarness();
        var context = harness.CreateContext("GET", "/api/projects");

        var result = await context.AuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        await context.ChallengeAsync();
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    // ── Harness ────────────────────────────────────────────────────────────────

    private static AuthHarness CreateHarness()
    {
        var tokenService = new FakeApiTokenService();
        tokenService.Add(ValidToken, OwnerId, "owner@sigil.test");
        tokenService.Revoke(RevokedToken);
        tokenService.Expire(ExpiredToken);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<IApiTokenService>(tokenService);
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = ApiTokenAuthenticationPolicy.SchemeName;
            options.DefaultChallengeScheme = ApiTokenAuthenticationPolicy.SchemeName;
        })
        .AddPolicyScheme(ApiTokenAuthenticationPolicy.SchemeName, "Sigil API token or cookie", options =>
        {
            options.ForwardDefaultSelector = ApiTokenAuthenticationPolicy.SelectScheme;
        })
        .AddScheme<ApiTokenAuthenticationOptions, ApiTokenAuthenticationHandler>(ApiTokenAuthenticationHandler.SchemeName, _ => { })
        .AddCookie(IdentityConstants.ApplicationScheme, options =>
        {
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
        });

        return new AuthHarness(services.BuildServiceProvider(), tokenService);
    }

    private sealed class AuthHarness(ServiceProvider provider, FakeApiTokenService tokenService)
    {
        public FakeApiTokenService TokenService { get; } = tokenService;

        public DefaultHttpContext CreateContext(string method, string path, string? bearer = null, string? cookie = null)
        {
            var scope = provider.CreateScope();
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Method = method;
            context.Request.Path = path;

            if (bearer is not null)
                context.Request.Headers["Authorization"] = $"Bearer {bearer}";

            if (cookie is not null)
                context.Request.Headers["Cookie"] = cookie;

            return context;
        }

        public async Task<string> CreateSessionCookie(Guid userId)
        {
            var scope = provider.CreateScope();
            var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            context.Request.Method = "GET";
            context.Request.Path = "/";

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "cookie");
            await context.SignInAsync(IdentityConstants.ApplicationScheme, new ClaimsPrincipal(identity));

            string setCookie = context.Response.Headers["Set-Cookie"].ToString();
            return setCookie.Split(';')[0];
        }
    }

    private sealed class FakeApiTokenService : IApiTokenService
    {
        private readonly Dictionary<string, ApiTokenIdentity> _tokens = new();
        private readonly HashSet<string> _revoked = new();
        private readonly HashSet<string> _expired = new();

        public void Add(string token, Guid userId, string email) => _tokens[token] = new ApiTokenIdentity(userId, email, []);

        public void Revoke(string token) => _revoked.Add(token);

        public void Expire(string token) => _expired.Add(token);

        public void AddRole(Guid userId, string role)
        {
            foreach (string token in _tokens.Keys.ToList())
            {
                ApiTokenIdentity identity = _tokens[token];
                if (identity.UserId == userId)
                    _tokens[token] = identity with { Roles = [role] };
            }
        }

        public Task<ApiTokenIdentity?> AuthenticateAsync(string presentedToken)
        {
            if (_revoked.Contains(presentedToken) || _expired.Contains(presentedToken))
                return Task.FromResult<ApiTokenIdentity?>(null);

            return Task.FromResult(_tokens.TryGetValue(presentedToken, out ApiTokenIdentity? identity) ? identity : null);
        }

        public Task<ApiTokenCreatedResponse> CreateAsync(Guid userId, string name, DateTime? expiresAt = null)
            => throw new NotSupportedException();

        public Task<List<ApiTokenSummary>> ListAsync(Guid userId) => Task.FromResult(new List<ApiTokenSummary>());

        public Task<bool> RevokeAsync(Guid userId, int tokenId) => Task.FromResult(false);
    }
}
