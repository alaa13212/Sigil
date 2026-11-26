using Sigil.Domain.Cors;

namespace Sigil.Domain.Tests.Cors;

public class CorsOriginsTests
{
    // ── Normalize ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("http://example.com", "http://example.com")]
    [InlineData("https://example.com/", "https://example.com")]
    [InlineData("HTTPS://Example.COM", "https://example.com")]
    [InlineData("https://example.com:443", "https://example.com")]
    [InlineData("http://example.com:80", "http://example.com")]
    [InlineData("http://example.com:3000", "http://example.com:3000")]
    [InlineData("https://example.com:8443", "https://example.com:8443")]
    [InlineData("http://[::1]:3000", "http://[::1]:3000")]
    [InlineData("http://[::1]", "http://[::1]")]
    [InlineData("*", "*")]
    public void Normalize_ValidOrigin_ReturnsCanonicalForm(string origin, string expected)
    {
        CorsOrigins.Normalize(origin).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("example.com")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("null")]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/path/")]
    [InlineData("https://example.com?query=1")]
    [InlineData("https://example.com#fragment")]
    [InlineData("https://user:pass@example.com")]
    [InlineData("https://a.com,https://b.com")]
    [InlineData("https://exa mple.com")]
    public void Normalize_InvalidOrigin_ReturnsNull(string? origin)
    {
        CorsOrigins.Normalize(origin).Should().BeNull();
    }

    [Fact]
    public void Normalize_TrimsSurroundingWhitespace()
    {
        CorsOrigins.Normalize("  https://example.com  ").Should().Be("https://example.com");
    }

    // ── Parse ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MultipleOrigins_ReturnsAll()
    {
        var origins = CorsOrigins.Parse("https://a.com,https://b.com");

        origins.Should().Equal("https://a.com", "https://b.com");
    }

    [Fact]
    public void Parse_DuplicateOrigins_ReturnsDistinct()
    {
        var origins = CorsOrigins.Parse("https://a.com,https://a.com,https://A.com");

        origins.Should().Equal("https://a.com");
    }

    [Fact]
    public void Parse_SkipsInvalidEntries()
    {
        var origins = CorsOrigins.Parse("https://a.com,not-an-origin,ftp://b.com");

        origins.Should().Equal("https://a.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyValue_ReturnsEmpty(string? value)
    {
        CorsOrigins.Parse(value).Should().BeEmpty();
    }

    // ── Split ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Split_SplitsOnCommasAndNewlines()
    {
        var parts = CorsOrigins.Split(["https://a.com", "https://b.com,https://c.com", "https://d.com\nhttps://e.com"]);

        parts.Should().Equal(
            "https://a.com",
            "https://b.com",
            "https://c.com",
            "https://d.com",
            "https://e.com");
    }

    [Fact]
    public void Split_NullValues_ReturnsEmpty()
    {
        CorsOrigins.Split(null).Should().BeEmpty();
    }

    [Fact]
    public void Split_BlankValues_AreSkipped()
    {
        CorsOrigins.Split([null, "", "   "]).Should().BeEmpty();
    }

    // ── IsMatch ───────────────────────────────────────────────────────────────

    [Fact]
    public void IsMatch_OriginInList_ReturnsTrue()
    {
        CorsOrigins.IsMatch(["https://a.com"], "https://a.com").Should().BeTrue();
    }

    [Fact]
    public void IsMatch_OriginNotInList_ReturnsFalse()
    {
        CorsOrigins.IsMatch(["https://a.com"], "https://b.com").Should().BeFalse();
    }

    [Fact]
    public void IsMatch_NormalizesBeforeComparing()
    {
        CorsOrigins.IsMatch(["https://example.com"], "HTTPS://EXAMPLE.COM").Should().BeTrue();
    }

    [Fact]
    public void IsMatch_EmptyList_ReturnsFalse()
    {
        CorsOrigins.IsMatch([], "https://a.com").Should().BeFalse();
    }

    [Fact]
    public void IsMatch_Wildcard_AllowsAnyValidOrigin()
    {
        CorsOrigins.IsMatch(["*"], "https://anything.com").Should().BeTrue();
    }

    [Fact]
    public void IsMatch_Wildcard_RejectsInvalidOrigin()
    {
        CorsOrigins.IsMatch(["*"], "not-an-origin").Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsMatch_MissingOrigin_ReturnsFalse(string? origin)
    {
        CorsOrigins.IsMatch(["https://a.com"], origin).Should().BeFalse();
    }
}
