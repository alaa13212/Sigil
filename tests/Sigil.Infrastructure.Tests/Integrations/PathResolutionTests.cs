using Sigil.Infrastructure.Integrations;

namespace Sigil.Infrastructure.Tests.Integrations;

public class PathResolutionTests
{
    [Fact]
    public void ExactMatch_ReturnsSamePath()
    {
        var paths = new[] { "src/MyApp/Services/FooService.cs", "src/Other/Bar.cs" };
        SourceCodeClientHelper.FindBestMatch(paths, "src/MyApp/Services/FooService.cs")
            .Should().Be("src/MyApp/Services/FooService.cs");
    }

    [Fact]
    public void SuffixMatch_FindsFileBySubProjectRelativePath()
    {
        var paths = new[] { "src/MyApp/Services/FooService.cs", "src/Other/Bar.cs" };
        SourceCodeClientHelper.FindBestMatch(paths, "Services/FooService.cs")
            .Should().Be("src/MyApp/Services/FooService.cs");
    }

    [Fact]
    public void FilenameOnlyMatch_FindsByFilename()
    {
        var paths = new[] { "src/MyApp/Services/FooService.cs", "src/Other/Bar.cs" };
        SourceCodeClientHelper.FindBestMatch(paths, "FooService.cs")
            .Should().Be("src/MyApp/Services/FooService.cs");
    }

    [Fact]
    public void FilenameOnlyMatch_PrefersShortestPath()
    {
        var paths = new[] { "src/MyApp/Services/FooService.cs", "vendor/some-lib/src/FooService.cs" };
        SourceCodeClientHelper.FindBestMatch(paths, "FooService.cs")
            .Should().Be("src/MyApp/Services/FooService.cs");
    }

    [Fact]
    public void CaseInsensitiveMatch_Works()
    {
        var paths = new[] { "Src/MyApp/Services/FooService.cs" };
        SourceCodeClientHelper.FindBestMatch(paths, "services/fooservice.cs")
            .Should().Be("Src/MyApp/Services/FooService.cs");
    }

    [Fact]
    public void BackslashNormalization_Works()
    {
        var paths = new[] { "src/MyApp/Services/FooService.cs" };
        SourceCodeClientHelper.FindBestMatch(paths, "Services\\FooService.cs")
            .Should().Be("src/MyApp/Services/FooService.cs");
    }

    [Fact]
    public void NoMatch_ReturnsNull()
    {
        var paths = new[] { "src/MyApp/Other.cs" };
        SourceCodeClientHelper.FindBestMatch(paths, "NonExistent.cs").Should().BeNull();
    }

    [Fact]
    public void EmptyTree_ReturnsNull()
    {
        SourceCodeClientHelper.FindBestMatch([], "FooService.cs").Should().BeNull();
    }

    [Theory]
    [InlineData("/src/app.py", "src/app.py")]
    [InlineData("\\src\\app.py", "src/app.py")]
    [InlineData("src/app.py", "src/app.py")]
    public void NormalizePath_StripLeadingSlashesAndNormalizeBackslashes(string input, string expected)
    {
        SourceCodeClientHelper.NormalizePath(input).Should().Be(expected);
    }

    [Fact]
    public void ExtractContext_ReturnsCorrectWindow()
    {
        var code = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"line {i}"));
        var lines = SourceCodeClientHelper.ExtractContext(code, lineNumber: 10, contextLines: 2);
        lines.Should().HaveCount(5); // lines 7..11
        lines[0].LineNumber.Should().Be(7);
        lines[4].LineNumber.Should().Be(11);
        lines[2].Content.Should().Be("line 10");
    }

    [Fact]
    public void ExtractContext_ClampsAtFileStart()
    {
        var code = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"line {i}"));
        var lines = SourceCodeClientHelper.ExtractContext(code, lineNumber: 2, contextLines: 5);
        lines[0].LineNumber.Should().Be(1); // clamped, not negative
    }

    [Fact]
    public void ExtractContext_ClampsAtFileEnd()
    {
        var code = string.Join("\n", Enumerable.Range(1, 5).Select(i => $"line {i}"));
        var lines = SourceCodeClientHelper.ExtractContext(code, lineNumber: 5, contextLines: 5);
        lines[^1].LineNumber.Should().Be(5); // clamped at end
    }
}
