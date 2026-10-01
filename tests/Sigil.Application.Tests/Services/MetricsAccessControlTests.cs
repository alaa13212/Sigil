using System.Reflection;
using System.Text;
using Sigil.Application.Services;

namespace Sigil.Application.Tests.Services;

public class MetricsAccessControlTests
{
    private const string Token = "s3cret-metrics-token";

    // ── Token extraction ──────────────────────────────────────────────────────

    [Fact]
    public void ExtractToken_ReadsBearerHeader()
    {
        MetricsAccessControl.ExtractToken("Bearer abc123", null).Should().Be("abc123");
    }

    [Fact]
    public void ExtractToken_BearerPrefixIsCaseInsensitive()
    {
        MetricsAccessControl.ExtractToken("bearer abc123", null).Should().Be("abc123");
    }

    [Fact]
    public void ExtractToken_FallsBackToTheQueryParameter()
    {
        MetricsAccessControl.ExtractToken(null, "abc123").Should().Be("abc123");
    }

    [Fact]
    public void ExtractToken_PrefersTheHeaderOverTheQueryParameter()
    {
        MetricsAccessControl.ExtractToken("Bearer from-header", "from-query").Should().Be("from-header");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("Basic abc123", null)]
    [InlineData("Bearer", null)]
    [InlineData("Bearer   ", null)]
    public void ExtractToken_ReturnsNullWhenNoUsableTokenIsPresent(string? header, string? query)
    {
        MetricsAccessControl.ExtractToken(header, query).Should().BeNull();
    }

    // ── Token comparison ──────────────────────────────────────────────────────

    [Fact]
    public void TokensMatch_AcceptsTheExactToken()
    {
        MetricsAccessControl.TokensMatch(Token, Token).Should().BeTrue();
    }

    [Theory]
    [InlineData("wrong-token-entirely")]
    [InlineData("s3cret-metrics-toke")]
    [InlineData("s3cret-metrics-tokenn")]
    [InlineData("S3CRET-METRICS-TOKEN")]
    [InlineData("")]
    public void TokensMatch_RejectsAnythingElse(string presented)
    {
        MetricsAccessControl.TokensMatch(Token, presented).Should().BeFalse();
    }

    [Fact]
    public void TokensMatch_RejectsWhenNoTokenIsConfigured()
    {
        MetricsAccessControl.TokensMatch(null, Token).Should().BeFalse();
        MetricsAccessControl.TokensMatch("", Token).Should().BeFalse();
    }

    [Fact]
    public void TokensMatch_RejectsAMissingPresentedToken()
    {
        MetricsAccessControl.TokensMatch(Token, null).Should().BeFalse();
    }

    /// <summary>Method tokens called by the body of <paramref name="method"/>.</summary>
    private static List<MethodBase?> CalledMethods(MethodInfo method)
    {
        byte[] il = method.GetMethodBody()?.GetILAsByteArray() ?? [];
        var called = new List<MethodBase?>();

        for (int i = 0; i + 4 < il.Length; i++)
        {
            if (il[i] is not (0x28 or 0x6F))
                continue;

            int token = il[i + 1] | (il[i + 2] << 8) | (il[i + 3] << 16) | (il[i + 4] << 24);
            try { called.Add(method.Module.ResolveMethod(token)); }
            catch (ArgumentException) { /* operand is not a method token */ }
        }

        return called;
    }

    private static MethodInfo TokensMatchMethod => typeof(MetricsAccessControl)
        .GetMethod(nameof(MetricsAccessControl.TokensMatch), BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void TokensMatch_UsesCryptographicOperationsFixedTimeEquals()
    {
        List<MethodInfo> called = CalledMethods(TokensMatchMethod)
            .OfType<MethodInfo>()
            .ToList();

        called.Should().Contain(m => m.DeclaringType == typeof(System.Security.Cryptography.CryptographicOperations)
            && m.Name == nameof(System.Security.Cryptography.CryptographicOperations.FixedTimeEquals));
    }

    [Fact]
    public void TokensMatch_DoesNotCompareWithStringEquality()
    {
        List<MethodInfo> called = CalledMethods(TokensMatchMethod)
            .OfType<MethodInfo>()
            .ToList();

        string[] comparisonMethods = ["op_Equality", "Equals", "Compare", "CompareOrdinal"];
        List<string> stringComparisons = called
            .Where(m => m.DeclaringType == typeof(string))
            .Select(m => m.Name)
            .Where(n => comparisonMethods.Contains(n))
            .ToList();

        stringComparisons.Should().BeEmpty();
    }

    // ── CIDR parsing and matching ─────────────────────────────────────────────

    [Fact]
    public void ParseAllowedCidrs_ParsesCommaSeparatedEntries()
    {
        MetricsAccessControl.ParseAllowedCidrs("10.0.0.0/8, 192.168.0.0/16")
            .Should().HaveCount(2);
    }

    [Fact]
    public void ParseAllowedCidrs_IgnoresUnparsableEntries()
    {
        MetricsAccessControl.ParseAllowedCidrs("not-a-cidr,10.0.0.0/8,999.999.0.0/8")
            .Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseAllowedCidrs_ReturnsEmptyForNoConfiguration(string? configured)
    {
        MetricsAccessControl.ParseAllowedCidrs(configured).Should().BeEmpty();
    }

    [Theory]
    [InlineData("10.1.2.3", true)]
    [InlineData("192.168.5.5", true)]
    [InlineData("8.8.8.8", false)]
    public void IsAddressAllowed_MatchesAgainstTheConfiguredNetworks(string address, bool expected)
    {
        var networks = MetricsAccessControl.ParseAllowedCidrs("10.0.0.0/8,192.168.0.0/16");

        MetricsAccessControl.IsAddressAllowed(address, networks).Should().Be(expected);
    }

    [Fact]
    public void IsAddressAllowed_MatchesIpv4MappedIpv6Addresses()
    {
        var networks = MetricsAccessControl.ParseAllowedCidrs("10.0.0.0/8");

        MetricsAccessControl.IsAddressAllowed("::ffff:10.1.2.3", networks).Should().BeTrue();
    }

    [Fact]
    public void IsAddressAllowed_RejectsEverythingWhenNoNetworksAreConfigured()
    {
        MetricsAccessControl.IsAddressAllowed("10.1.2.3", []).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-ip")]
    public void IsAddressAllowed_RejectsAnUnparsableAddress(string? address)
    {
        MetricsAccessControl.IsAddressAllowed(address, MetricsAccessControl.ParseAllowedCidrs("10.0.0.0/8"))
            .Should().BeFalse();
    }

    // ── The gate itself ───────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_DeniesEverythingWhenNothingIsConfigured()
    {
        MetricsAccessControl.Evaluate(null, null, null, "10.1.2.3")
            .Should().Be(MetricsAccess.NotConfigured);
    }

    [Fact]
    public void Evaluate_DeniesAnUnauthenticatedScrapeWhenNothingIsConfigured()
    {
        MetricsAccessControl.Evaluate(null, null, "guessed-token", "8.8.8.8")
            .Should().Be(MetricsAccess.NotConfigured);
    }

    [Fact]
    public void Evaluate_AllowsAValidBearerToken()
    {
        MetricsAccessControl.Evaluate(Token, null, Token, "8.8.8.8")
            .Should().Be(MetricsAccess.Allowed);
    }

    [Fact]
    public void Evaluate_RejectsAMissingToken()
    {
        MetricsAccessControl.Evaluate(Token, null, null, "8.8.8.8")
            .Should().Be(MetricsAccess.MissingCredentials);
    }

    [Fact]
    public void Evaluate_RejectsAWrongToken()
    {
        MetricsAccessControl.Evaluate(Token, null, "nope", "8.8.8.8")
            .Should().Be(MetricsAccess.InvalidCredentials);
    }

    [Fact]
    public void Evaluate_AllowsARequestFromAnAllowedNetworkWithoutAToken()
    {
        MetricsAccessControl.Evaluate(null, "10.0.0.0/8", null, "10.1.2.3")
            .Should().Be(MetricsAccess.Allowed);
    }

    [Fact]
    public void Evaluate_RejectsARequestFromOutsideTheAllowedNetwork()
    {
        MetricsAccessControl.Evaluate(null, "10.0.0.0/8", null, "8.8.8.8")
            .Should().Be(MetricsAccess.MissingCredentials);
    }

    [Fact]
    public void Evaluate_AllowsATokenHolderFromOutsideTheAllowedNetwork()
    {
        MetricsAccessControl.Evaluate(Token, "10.0.0.0/8", Token, "8.8.8.8")
            .Should().Be(MetricsAccess.Allowed);
    }

    [Fact]
    public void Evaluate_RejectsAWrongTokenEvenFromInsideTheAllowedNetworkCheck()
    {
        MetricsAccessControl.Evaluate(Token, "192.168.0.0/16", "nope", "8.8.8.8")
            .Should().Be(MetricsAccess.InvalidCredentials);
    }

    [Fact]
    public void Evaluate_AnUnparsableCidrListLeavesTheEndpointUnconfigured()
    {
        MetricsAccessControl.Evaluate(null, "garbage", null, "10.1.2.3")
            .Should().Be(MetricsAccess.NotConfigured);
    }

    // ── Status codes ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(MetricsAccess.Allowed, 200)]
    [InlineData(MetricsAccess.NotConfigured, 403)]
    [InlineData(MetricsAccess.MissingCredentials, 401)]
    [InlineData(MetricsAccess.InvalidCredentials, 403)]
    public void ToStatusCode_MapsEachDecision(MetricsAccess access, int expected)
    {
        MetricsAccessControl.ToStatusCode(access).Should().Be(expected);
    }

    [Fact]
    public void EveryDenialIsNonSuccess()
    {
        foreach (MetricsAccess access in Enum.GetValues<MetricsAccess>().Where(a => a != MetricsAccess.Allowed))
            MetricsAccessControl.ToStatusCode(access).Should().NotBe(200);
    }

    // ── Configuration keys ────────────────────────────────────────────────────

    [Fact]
    public void TokenFromConfigComparesEqualToThePresentedBearerValue()
    {
        string configured = Sigil.Domain.AppConfigKeys.MetricsToken;

        MetricsAccessControl.TokensMatch(configured, configured).Should().BeTrue();
        MetricsAccessControl.TokensMatch(configured, "metrics_token ").Should().BeFalse();
    }

    [Fact]
    public void ConfiguredTokenIsNotTheEmptyString()
    {
        Sigil.Domain.AppConfigKeys.MetricsToken.Should().Be("metrics_token");
        Sigil.Domain.AppConfigKeys.MetricsAllowedCidrs.Should().Be("metrics_allowed_cidrs");
    }

    [Fact]
    public void TokensMatch_IsUnaffectedBySurroundingWhitespaceInTheHeader()
    {
        // The bearer value is trimmed during extraction, so a padded header still authenticates.
        string extracted = MetricsAccessControl.ExtractToken("Bearer   " + Token + "  ", null)!;

        extracted.Should().Be(Token);
        MetricsAccessControl.TokensMatch(Token, extracted).Should().BeTrue();
    }

    [Fact]
    public void TokensMatch_HandlesMultiByteTokens()
    {
        var token = new string('é', 16);

        MetricsAccessControl.TokensMatch(token, token).Should().BeTrue();
        MetricsAccessControl.TokensMatch(token, token + "x").Should().BeFalse();
        MetricsAccessControl.TokensMatch(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(token)), token)
            .Should().BeTrue();
    }
}
