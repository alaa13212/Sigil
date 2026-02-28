using Sigil.Application.Interfaces;
using Sigil.Application.Services;
using Sigil.Domain.Enums;
using Sigil.Domain.Ingestion;

namespace Sigil.Application.Tests.Services;

public class DefaultFingerprintGeneratorTests
{
    private readonly IHashGenerator _hashGenerator;
    private readonly DefaultFingerprintGenerator _generator;

    public DefaultFingerprintGeneratorTests()
    {
        // Use a real-ish hash function for determinism testing
        _hashGenerator = Substitute.For<IHashGenerator>();
        _hashGenerator.ComputeHash(Arg.Any<string>()).Returns(x => $"hash:{x.Arg<string>()}");
        _generator = new DefaultFingerprintGenerator(_hashGenerator);
    }

    private static ParsedEvent MakeEvent(
        string? exceptionType = "System.Exception",
        string? normalizedMessage = "Something went wrong",
        List<ParsedStackFrame>? frames = null,
        IReadOnlyList<string>? fingerprintHints = null) => new()
    {
        EventId = Guid.NewGuid().ToString(),
        Timestamp = DateTime.UtcNow,
        Platform = Platform.CSharp,
        Level = Severity.Error,
        RawJson = "{}",
        NormalizedMessage = normalizedMessage,
        ExceptionType = exceptionType,
        Stacktrace = frames ?? [],
        FingerprintHints = fingerprintHints,
    };

    [Fact]
    public void GenerateFingerprint_SameInputs_ReturnsSameFingerprint()
    {
        var frames = new List<ParsedStackFrame>
        {
            new() { Filename = "Program.cs", Function = "Main", InApp = true },
        };
        var ev1 = MakeEvent(frames: frames);
        var ev2 = MakeEvent(frames: frames);

        _generator.GenerateFingerprint(ev1).Should().Be(_generator.GenerateFingerprint(ev2));
    }

    [Fact]
    public void GenerateFingerprint_DifferentExceptionType_ReturnsDifferentFingerprint()
    {
        var ev1 = MakeEvent(exceptionType: "System.NullReferenceException");
        var ev2 = MakeEvent(exceptionType: "System.ArgumentException");

        _generator.GenerateFingerprint(ev1).Should().NotBe(_generator.GenerateFingerprint(ev2));
    }

    [Fact]
    public void GenerateFingerprint_NullMessage_DoesNotThrow()
    {
        var ev = MakeEvent(normalizedMessage: null);
        var act = () => _generator.GenerateFingerprint(ev);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateFingerprint_NullExceptionType_DoesNotThrow()
    {
        var ev = MakeEvent(exceptionType: null);
        var act = () => _generator.GenerateFingerprint(ev);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateFingerprint_EmptyStacktrace_DoesNotThrow()
    {
        var ev = MakeEvent(frames: []);
        var act = () => _generator.GenerateFingerprint(ev);
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateFingerprint_InAppFramesFiltered_OnlyInAppUsed()
    {
        var inAppFrames = new List<ParsedStackFrame>
        {
            new() { Filename = "App.cs", Function = "Run", InApp = true },
        };
        var mixedFrames = new List<ParsedStackFrame>
        {
            new() { Filename = "External.cs", Function = "DoThing", InApp = false },
            new() { Filename = "App.cs", Function = "Run", InApp = true },
        };

        var fpInApp = _generator.GenerateFingerprint(MakeEvent(frames: inAppFrames));
        var fpMixed = _generator.GenerateFingerprint(MakeEvent(frames: mixedFrames));

        fpInApp.Should().Be(fpMixed);
    }

    [Fact]
    public void GenerateFingerprint_HintsWithDefaultPlaceholder_UsesHintsArrayDirectly()
    {
        // When hints contain "{{ default }}", the hints array is used verbatim as fingerprint parts
        var ev = MakeEvent(fingerprintHints: ["{{ default }}", "extra-part"]);
        var result = _generator.GenerateFingerprint(ev);
        result.Should().Be("hash:{{ default }}|extra-part");
    }

    [Fact]
    public void GenerateFingerprint_HintsWithoutDefaultPlaceholder_UsesEventExtraction()
    {
        // Without "{{ default }}", hints are treated as "no customization" — event extraction is used
        var evWithHints = MakeEvent(fingerprintHints: ["custom-group"]);
        var evNoHints = MakeEvent(fingerprintHints: null);
        // Both should produce the same fingerprint since they share the same exception/message
        _generator.GenerateFingerprint(evWithHints).Should().Be(_generator.GenerateFingerprint(evNoHints));
    }

    [Fact]
    public void GenerateFingerprint_CallsHashGenerator()
    {
        var ev = MakeEvent();
        _generator.GenerateFingerprint(ev);
        _hashGenerator.Received(1).ComputeHash(Arg.Any<string>());
    }
}
