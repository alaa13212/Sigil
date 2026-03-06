using Microsoft.AspNetCore.Http;
using Sigil.Application.Services;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Tests.Services;

public class RemoteIpProviderEnricherTests
{
    private static EventParsingContext DefaultContext() => new()
    {
        ProjectId = 1,
        NormalizationRules = [],
        AutoTagRules = [],
        InboundFilters = [],
        StackTraceFilters = [],
    };

    private static ParsedEvent MakeParsedEvent(ParsedEventUser? user) => new()
    {
        EventId = "test",
        Timestamp = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = Severity.Error,
        RawJson = "{}",
        User = user,
    };

    [Fact]
    public void Enrich_UserWithAutoIp_ResolvesFromHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.42");
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        var enricher = new RemoteIpProviderEnricher(accessor);
        var parsed = MakeParsedEvent(new ParsedEventUser { IpAddress = "{{auto}}" });

        enricher.Enrich(parsed, DefaultContext());

        parsed.User!.IpAddress.Should().Be("192.168.1.42");
    }

    [Fact]
    public void Enrich_UserWithAutoIp_NoRemoteAddress_SetsNull()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);
        var enricher = new RemoteIpProviderEnricher(accessor);
        var parsed = MakeParsedEvent(new ParsedEventUser { IpAddress = "{{auto}}" });

        enricher.Enrich(parsed, DefaultContext());

        parsed.User!.IpAddress.Should().BeNull();
    }

    [Fact]
    public void Enrich_UserWithAutoIp_NoHttpContext_SetsNull()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);
        var enricher = new RemoteIpProviderEnricher(accessor);
        var parsed = MakeParsedEvent(new ParsedEventUser { IpAddress = "{{auto}}" });

        enricher.Enrich(parsed, DefaultContext());

        parsed.User!.IpAddress.Should().BeNull();
    }

    [Fact]
    public void Enrich_UserWithRealIp_NotModified()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var enricher = new RemoteIpProviderEnricher(accessor);
        var parsed = MakeParsedEvent(new ParsedEventUser { IpAddress = "10.0.0.1" });

        enricher.Enrich(parsed, DefaultContext());

        parsed.User!.IpAddress.Should().Be("10.0.0.1");
    }

    [Fact]
    public void Enrich_NullUser_NoOp()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        var enricher = new RemoteIpProviderEnricher(accessor);
        var parsed = MakeParsedEvent(null);

        enricher.Enrich(parsed, DefaultContext());

        parsed.User.Should().BeNull();
    }
}
