using Sigil.Application.Services;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Tests.Services;

public class PredefinedTagsEnricherTests
{
    private readonly PredefinedTagsEnricher _enricher = new();

    private static ParsedEvent MakeEvent(
        string? environment = null,
        string? release = null,
        Severity level = Severity.Error,
        string? serverName = null,
        Runtime? runtime = null) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = level,
        RawJson = "{}",
        Environment = environment,
        Release = release,
        ServerName = serverName,
        Runtime = runtime,
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
    public void Enrich_EnvironmentSet_TagAdded()
    {
        var evt = MakeEvent(environment: "production");
        _enricher.Enrich(evt, MakeContext());

        evt.Tags!["environment"].Should().Be("production");
    }

    [Fact]
    public void Enrich_EnvironmentNull_TagNotAdded()
    {
        var evt = MakeEvent();
        _enricher.Enrich(evt, MakeContext());

        evt.Tags!.Should().NotContainKey("environment");
    }

    [Fact]
    public void Enrich_ReleaseSet_TagAdded()
    {
        var evt = MakeEvent(release: "v1.0.0");
        _enricher.Enrich(evt, MakeContext());

        evt.Tags!["release"].Should().Be("v1.0.0");
    }

    [Fact]
    public void Enrich_ReleaseNull_TagNotAdded()
    {
        var evt = MakeEvent();
        _enricher.Enrich(evt, MakeContext());

        evt.Tags!.Should().NotContainKey("release");
    }

    [Fact]
    public void Enrich_LevelAlwaysAdded()
    {
        var evt = MakeEvent(level: Severity.Warning);
        _enricher.Enrich(evt, MakeContext());

        evt.Tags!["level"].Should().Be("warning");
    }

    [Fact]
    public void Enrich_ServerNameSet_TagAdded()
    {
        var evt = MakeEvent(serverName: "web-01");
        _enricher.Enrich(evt, MakeContext());

        evt.Tags!["server_name"].Should().Be("web-01");
    }

    [Fact]
    public void Enrich_ServerNameNull_TagNotAdded()
    {
        var evt = MakeEvent();
        _enricher.Enrich(evt, MakeContext());

        evt.Tags!.Should().NotContainKey("server_name");
    }

    [Fact]
    public void Enrich_RuntimeSet_BothTagsAdded()
    {
        var evt = MakeEvent(runtime: new Runtime(".NET", "8.0.0"));
        _enricher.Enrich(evt, MakeContext());

        evt.Tags!["runtime"].Should().Be(".NET 8.0.0");
        evt.Tags!["runtime.name"].Should().Be(".NET");
    }

    [Fact]
    public void Enrich_RuntimeNull_TagsNotAdded()
    {
        var evt = MakeEvent();
        _enricher.Enrich(evt, MakeContext());

        evt.Tags!.Should().NotContainKey("runtime");
        evt.Tags!.Should().NotContainKey("runtime.name");
    }

    [Fact]
    public void Enrich_NullTags_InitializesDict()
    {
        var evt = MakeEvent();
        evt.Tags = null;
        _enricher.Enrich(evt, MakeContext());

        evt.Tags.Should().NotBeNull();
    }
}
