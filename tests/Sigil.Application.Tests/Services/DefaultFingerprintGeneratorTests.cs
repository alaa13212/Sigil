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
    public void GenerateFingerprint_HintsWithDefaultPlaceholder_ReplacesPlaceholderWithEventParts()
    {
        // "{{ default }}" is replaced by event-extracted parts; other hints are blended around them
        var ev = MakeEvent(
            exceptionType: "System.Exception",
            normalizedMessage: "Something went wrong",
            frames: [],
            fingerprintHints: ["{{ default }}", "extra-part"]);
        var result = _generator.GenerateFingerprint(ev);
        // Event parts are ["System.Exception", "Something went wrong"], then "extra-part" appended
        result.Should().Be("hash:System.Exception|Something went wrong|extra-part");
    }

    [Fact]
    public void GenerateFingerprint_HintsWithoutDefaultPlaceholder_UsesHintsVerbatim()
    {
        // Hints without "{{ default }}" are used as the complete fingerprint (no event extraction)
        var evWithHints = MakeEvent(fingerprintHints: ["custom-group"]);
        var result = _generator.GenerateFingerprint(evWithHints);
        result.Should().Be("hash:custom-group");
    }

    [Fact]
    public void GenerateFingerprint_HintsWithoutDefaultPlaceholder_DiffersFromEventFingerprint()
    {
        // Pure client hints produce a different fingerprint than the event-derived one
        var evWithHints = MakeEvent(fingerprintHints: ["custom-group"]);
        var evNoHints   = MakeEvent(fingerprintHints: null);
        _generator.GenerateFingerprint(evWithHints).Should().NotBe(_generator.GenerateFingerprint(evNoHints));
    }

    [Fact]
    public void GenerateFingerprint_CallsHashGenerator()
    {
        var ev = MakeEvent();
        _generator.GenerateFingerprint(ev);
        _hashGenerator.Received(1).ComputeHash(Arg.Any<string>());
    }

    [Fact]
    public void GenerateFingerprint_NullExceptionType_UsesFallback()
    {
        // null ExceptionType → "unknown-exception" fallback (not "")
        var ev = MakeEvent(exceptionType: null, normalizedMessage: "msg", frames: []);
        _generator.GenerateFingerprint(ev).Should().Be("hash:unknown-exception|msg");
    }

    [Fact]
    public void GenerateFingerprint_NullMessage_FallsBackToNoMessage()
    {
        // null NormalizedMessage → "no-message" fallback (not "")
        var ev = MakeEvent(exceptionType: "Ex", normalizedMessage: null, frames: []);
        _generator.GenerateFingerprint(ev).Should().Contain("no-message");
    }

    [Fact]
    public void GenerateFingerprint_NullMessage_DiffersFromNonNullMessage()
    {
        // NormalizedMessage is actually used (not always replaced by fallback)
        var evNull = MakeEvent(exceptionType: "Ex", normalizedMessage: null, frames: []);
        var evMsg  = MakeEvent(exceptionType: "Ex", normalizedMessage: "actual msg", frames: []);
        _generator.GenerateFingerprint(evNull).Should().NotBe(_generator.GenerateFingerprint(evMsg));
    }

    [Fact]
    public void GenerateFingerprint_DifferentMessage_ReturnsDifferentFingerprint()
    {
        // message contributes to the hash input
        var ev1 = MakeEvent(exceptionType: "System.Exception", normalizedMessage: "Error A");
        var ev2 = MakeEvent(exceptionType: "System.Exception", normalizedMessage: "Error B");
        _generator.GenerateFingerprint(ev1).Should().NotBe(_generator.GenerateFingerprint(ev2));
    }

    [Fact]
    public void GenerateFingerprint_FrameWithoutFilename_Excluded()
    {
        // frames missing Filename are filtered out; event with filename differs from one without
        var withFilename    = new List<ParsedStackFrame> { new() { Filename = "App.cs", Function = "Run" } };
        var withoutFilename = new List<ParsedStackFrame> { new() { Filename = null,    Function = "Run" } };
        var fp1 = _generator.GenerateFingerprint(MakeEvent(frames: withFilename));
        var fp2 = _generator.GenerateFingerprint(MakeEvent(frames: withoutFilename));
        fp1.Should().NotBe(fp2);
    }

    [Fact]
    public void GenerateFingerprint_FrameWithoutFunction_Excluded()
    {
        // frames missing Function are filtered out
        var withFunction    = new List<ParsedStackFrame> { new() { Filename = "App.cs", Function = "Run" } };
        var withoutFunction = new List<ParsedStackFrame> { new() { Filename = "App.cs", Function = null } };
        var fp1 = _generator.GenerateFingerprint(MakeEvent(frames: withFunction));
        var fp2 = _generator.GenerateFingerprint(MakeEvent(frames: withoutFunction));
        fp1.Should().NotBe(fp2);
    }

    [Fact]
    public void GenerateFingerprint_FrameIncludedInOutput()
    {
        // frame function and filename both appear in the hash input
        var ev = MakeEvent(
            exceptionType: "Ex",
            normalizedMessage: "msg",
            frames: [new() { Filename = "App.cs", Function = "Run" }]);
        _generator.GenerateFingerprint(ev).Should().Contain("Run@App.cs");
    }

    [Fact]
    public void GenerateFingerprint_TakesLastFiveFrames()
    {
        // TakeLast(5): unique first frame should not appear; last frames should
        var frames = new List<ParsedStackFrame>
        {
            new() { Filename = "a.cs", Function = "UniqueFirst" },
            new() { Filename = "b.cs", Function = "Frame1" },
            new() { Filename = "b.cs", Function = "Frame2" },
            new() { Filename = "b.cs", Function = "Frame3" },
            new() { Filename = "b.cs", Function = "Frame4" },
            new() { Filename = "b.cs", Function = "Frame5" },
        };
        var fp = _generator.GenerateFingerprint(MakeEvent(frames: frames));
        fp.Should().NotContain("UniqueFirst"); // dropped by TakeLast(5)
        fp.Should().Contain("Frame5");
    }

    [Fact]
    public void GenerateFingerprint_MultipleHintsNoDefault_PreservesInsertionOrder()
    {
        var ev = MakeEvent(
            exceptionType: "Ex", normalizedMessage: "msg", frames: [],
            fingerprintHints: ["hint1", "hint2"]);
        var result = _generator.GenerateFingerprint(ev);
        result.Should().Be("hash:hint1|hint2");
    }

    [Fact]
    public void GenerateFingerprint_MultipleHintsBeforeDefault_InsertsInOrder()
    {
        // Two hints before {{ default }}: i++ puts hint1 at 0, hint2 at 1 (via Insert), then event parts follow.
        // i-- mutation: Insert(0, hint1) then Insert(-1, hint2) → ArgumentOutOfRangeException.
        var ev = MakeEvent(
            exceptionType: "Ex", normalizedMessage: "msg", frames: [],
            fingerprintHints: ["hint1", "hint2", "{{ default }}"]);
        var result = _generator.GenerateFingerprint(ev);
        result.Should().Be("hash:hint1|hint2|Ex|msg");
    }
}
