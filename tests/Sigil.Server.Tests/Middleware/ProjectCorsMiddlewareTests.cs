using Microsoft.AspNetCore.Http;
using Sigil.Application.Interfaces;
using Sigil.Server.Middleware;

namespace Sigil.Server.Tests.Middleware;

public class ProjectCorsMiddlewareTests
{
    private const int ProjectId = 42;
    private const string EnvelopePath = "/api/42/envelope";
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";
    private const string AllowMethodsHeader = "Access-Control-Allow-Methods";
    private const string AllowHeadersHeader = "Access-Control-Allow-Headers";
    private const string MaxAgeHeader = "Access-Control-Max-Age";

    private static IProjectConfigService ConfigService(params string[] origins)
    {
        var service = Substitute.For<IProjectConfigService>();
        service.AllowedCorsOrigins(Arg.Any<int>()).Returns([]);
        service.AllowedCorsOrigins(ProjectId).Returns(origins);
        return service;
    }

    private static async Task<(HttpContext Context, bool NextCalled)> InvokeAsync(
        IProjectConfigService configService,
        string method,
        string path,
        string? origin = null,
        string? requestedMethod = null,
        string? requestedHeaders = null,
        string scheme = "https",
        string host = "sigil.example.com")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);

        if (origin is not null)
            context.Request.Headers["Origin"] = origin;

        if (requestedMethod is not null)
            context.Request.Headers["Access-Control-Request-Method"] = requestedMethod;

        if (requestedHeaders is not null)
            context.Request.Headers["Access-Control-Request-Headers"] = requestedHeaders;

        bool nextCalled = false;
        var middleware = new ProjectCorsMiddleware(_ =>
        {
            nextCalled = true;
            context.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        }, configService);

        await middleware.InvokeAsync(context);

        return (context, nextCalled);
    }

    [Fact]
    public async Task NoOriginHeader_PassesThroughWithoutCorsHeaders()
    {
        var (context, nextCalled) = await InvokeAsync(ConfigService("https://app.example.com"), "POST", EnvelopePath);

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers.ContainsKey(AllowOriginHeader).Should().BeFalse();
    }

    [Theory]
    [InlineData("/api/projects/42/issues")]
    [InlineData("/api/42/envelopes")]
    [InlineData("/api/notanumber/envelope")]
    [InlineData("/")]
    public async Task NonIngestionPath_PassesThroughWithoutCorsHeaders(string path)
    {
        var (context, nextCalled) = await InvokeAsync(ConfigService(), "GET", path, "https://evil.example.com");

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers.ContainsKey(AllowOriginHeader).Should().BeFalse();
    }

    [Fact]
    public async Task IngestionPath_TrailingSlash_IsTreatedAsIngestion()
    {
        var (_, nextCalled) = await InvokeAsync(ConfigService(), "POST", "/api/42/envelope/", "https://evil.example.com");

        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Preflight_AllowedOrigin_Returns204WithCorsHeaders()
    {
        var (context, nextCalled) = await InvokeAsync(
            ConfigService("https://app.example.com"),
            "OPTIONS",
            EnvelopePath,
            "https://app.example.com",
            requestedMethod: "POST",
            requestedHeaders: "x-sentry-auth,content-type");

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        context.Response.Headers[AllowOriginHeader].ToString().Should().Be("https://app.example.com");
        context.Response.Headers[AllowMethodsHeader].ToString().Should().Be("POST, OPTIONS");
        context.Response.Headers[AllowHeadersHeader].ToString().Should().Be("x-sentry-auth,content-type");
        context.Response.Headers[MaxAgeHeader].ToString().Should().Be("600");
        context.Response.Headers["Vary"].ToString().Should().Contain("Origin");
    }

    [Fact]
    public async Task Preflight_WithoutRequestedHeaders_UsesDefaultHeaderSet()
    {
        var (context, _) = await InvokeAsync(
            ConfigService("https://app.example.com"),
            "OPTIONS",
            EnvelopePath,
            "https://app.example.com",
            requestedMethod: "POST");

        context.Response.Headers[AllowHeadersHeader].ToString().Should().Contain("X-Sentry-Auth");
        context.Response.Headers[AllowHeadersHeader].ToString().Should().Contain("Content-Type");
    }

    [Fact]
    public async Task Preflight_BlockedOrigin_Returns403WithoutCorsHeaders()
    {
        var (context, nextCalled) = await InvokeAsync(
            ConfigService("https://app.example.com"),
            "OPTIONS",
            EnvelopePath,
            "https://evil.example.com",
            requestedMethod: "POST");

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.Headers.ContainsKey(AllowOriginHeader).Should().BeFalse();
        context.Response.Headers.ContainsKey(AllowMethodsHeader).Should().BeFalse();
    }

    [Fact]
    public async Task Post_AllowedOrigin_ContinuesWithAllowOriginHeader()
    {
        var (context, nextCalled) = await InvokeAsync(
            ConfigService("https://app.example.com"),
            "POST",
            EnvelopePath,
            "https://app.example.com");

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers[AllowOriginHeader].ToString().Should().Be("https://app.example.com");
    }

    [Fact]
    public async Task Post_BlockedOrigin_Returns403WithoutCorsHeaders()
    {
        var (context, nextCalled) = await InvokeAsync(
            ConfigService("https://app.example.com"),
            "POST",
            EnvelopePath,
            "https://evil.example.com");

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.Headers.ContainsKey(AllowOriginHeader).Should().BeFalse();
    }

    [Fact]
    public async Task Post_EmptyWhitelist_Returns403()
    {
        var (context, nextCalled) = await InvokeAsync(ConfigService(), "POST", EnvelopePath, "https://app.example.com");

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Post_WildcardWhitelist_AllowsAnyOrigin()
    {
        var (context, nextCalled) = await InvokeAsync(
            ConfigService("*"),
            "POST",
            EnvelopePath,
            "https://anything.example.com");

        nextCalled.Should().BeTrue();
        context.Response.Headers[AllowOriginHeader].ToString().Should().Be("https://anything.example.com");
    }

    [Fact]
    public async Task Post_MalformedOrigin_Returns403WithoutCorsHeaders()
    {
        var (context, nextCalled) = await InvokeAsync(
            ConfigService("*"),
            "POST",
            EnvelopePath,
            "not-an-origin");

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.Headers.ContainsKey(AllowOriginHeader).Should().BeFalse();
    }

    [Fact]
    public async Task Post_SameOrigin_PassesThroughWithoutCorsHeaders()
    {
        var (context, nextCalled) = await InvokeAsync(
            ConfigService(),
            "POST",
            EnvelopePath,
            "https://sigil.example.com");

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Headers.ContainsKey(AllowOriginHeader).Should().BeFalse();
    }

    [Fact]
    public async Task Post_SameOriginDifferentPort_IsNotSameOrigin()
    {
        var (context, nextCalled) = await InvokeAsync(
            ConfigService(),
            "POST",
            EnvelopePath,
            "https://sigil.example.com:8443");

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task Options_WithoutRequestMethodHeader_IsNotTreatedAsPreflight()
    {
        var (context, nextCalled) = await InvokeAsync(
            ConfigService("https://app.example.com"),
            "OPTIONS",
            EnvelopePath,
            "https://app.example.com");

        nextCalled.Should().BeTrue();
        context.Response.Headers.ContainsKey(AllowMethodsHeader).Should().BeFalse();
        context.Response.Headers[AllowOriginHeader].ToString().Should().Be("https://app.example.com");
    }

    [Fact]
    public async Task AllowedOrigin_IsNormalizedBeforeMatching()
    {
        var (context, nextCalled) = await InvokeAsync(
            ConfigService("https://app.example.com"),
            "POST",
            EnvelopePath,
            "HTTPS://APP.EXAMPLE.COM");

        nextCalled.Should().BeTrue();
        context.Response.Headers[AllowOriginHeader].ToString().Should().Be("https://app.example.com");
    }

    [Fact]
    public async Task Whitelist_IsReadForTheProjectInThePath()
    {
        var service = Substitute.For<IProjectConfigService>();
        service.AllowedCorsOrigins(Arg.Any<int>()).Returns([]);
        service.AllowedCorsOrigins(99).Returns(["https://other.example.com"]);

        var (context, nextCalled) = await InvokeAsync(service, "POST", "/api/42/envelope", "https://other.example.com");

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
