using Sigil.Application.Services;
using Sigil.Domain.Entities;

namespace Sigil.Application.Tests.Services;

public class MessageNormalizerTests
{
    private readonly MessageNormalizer _normalizer = new();

    private static TextNormalizationRule MakeRule(string pattern, string replacement) => new()
    {
        Pattern = pattern,
        Replacement = replacement,
        Priority = 0,
        Enabled = true,
    };

    [Fact]
    public void NormalizeMessage_EmptyRules_ReturnsOriginal()
    {
        var result = _normalizer.NormalizeMessage([], "Hello 123");

        result.Should().Be("Hello 123");
    }

    [Fact]
    public void NormalizeMessage_SingleRule_Replaces()
    {
        var rules = new[] { MakeRule(@"\d+", "<NUM>") };

        var result = _normalizer.NormalizeMessage(rules, "Error code 42");

        result.Should().Be("Error code <NUM>");
    }

    [Fact]
    public void NormalizeMessage_MultipleRules_AppliedInSequence()
    {
        var rules = new[]
        {
            MakeRule(@"\d+", "<NUM>"),
            MakeRule(@"<NUM>", "X"),
        };

        var result = _normalizer.NormalizeMessage(rules, "Error 42");

        result.Should().Be("Error X");
    }

    [Fact]
    public void NormalizeMessage_NonMatchingRule_ReturnsOriginal()
    {
        var rules = new[] { MakeRule(@"xyz", "replaced") };

        var result = _normalizer.NormalizeMessage(rules, "Hello world");

        result.Should().Be("Hello world");
    }
}
