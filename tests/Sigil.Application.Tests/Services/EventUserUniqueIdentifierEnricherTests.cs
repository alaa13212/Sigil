using Sigil.Application.Interfaces;
using Sigil.Application.Services;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Tests.Services;

public class EventUserUniqueIdentifierEnricherTests
{
    private readonly IHashGenerator _hashGenerator;
    private readonly EventUserUniqueIdentifierEnricher _enricher;

    public EventUserUniqueIdentifierEnricherTests()
    {
        _hashGenerator = Substitute.For<IHashGenerator>();
        _hashGenerator.ComputeHash(Arg.Any<string>()).Returns(x => $"hash:{x.Arg<string>()}");
        _enricher = new EventUserUniqueIdentifierEnricher(_hashGenerator);
    }

    private static ParsedEvent MakeEvent(ParsedEventUser? user = null) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = Severity.Error,
        RawJson = "{}",
        User = user,
    };

    private static EventParsingContext MakeContext() => new()
    {
        ProjectId = 1,
        NormalizationRules = [],
        AutoTagRules = [],
        InboundFilters = [],
        StackTraceFilters = [],
    };

    [Fact]
    public void Enrich_UserNull_NoOp()
    {
        var evt = MakeEvent(user: null);
        _enricher.Enrich(evt, MakeContext());

        evt.User.Should().BeNull();
        _hashGenerator.DidNotReceive().ComputeHash(Arg.Any<string>());
    }

    [Fact]
    public void Enrich_AllFieldsEmpty_SetsUserToNull()
    {
        var evt = MakeEvent(user: new ParsedEventUser());
        _enricher.Enrich(evt, MakeContext());

        evt.User.Should().BeNull();
    }

    [Fact]
    public void Enrich_IdPresent_HashesId()
    {
        var evt = MakeEvent(user: new ParsedEventUser { Id = "user-1", Email = "a@b.com" });
        _enricher.Enrich(evt, MakeContext());

        evt.User!.UniqueIdentifier.Should().Be("hash:user-1");
        _hashGenerator.Received(1).ComputeHash("user-1");
    }

    [Fact]
    public void Enrich_NoId_EmailUsed()
    {
        var evt = MakeEvent(user: new ParsedEventUser { Email = "a@b.com", Username = "user" });
        _enricher.Enrich(evt, MakeContext());

        evt.User!.UniqueIdentifier.Should().Be("hash:a@b.com");
    }

    [Fact]
    public void Enrich_NoIdOrEmail_UsernameUsed()
    {
        var evt = MakeEvent(user: new ParsedEventUser { Username = "user" });
        _enricher.Enrich(evt, MakeContext());

        evt.User!.UniqueIdentifier.Should().Be("hash:user");
    }

    [Fact]
    public void Enrich_OnlyIpAddress_IpUsed()
    {
        var evt = MakeEvent(user: new ParsedEventUser { IpAddress = "1.2.3.4" });
        _enricher.Enrich(evt, MakeContext());

        evt.User!.UniqueIdentifier.Should().Be("hash:1.2.3.4");
    }
}
