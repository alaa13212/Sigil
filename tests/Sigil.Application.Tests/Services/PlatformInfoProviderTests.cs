using Sigil.Application.Services;
using Sigil.Domain.Enums;

namespace Sigil.Application.Tests.Services;

public class PlatformInfoProviderTests
{
    private readonly PlatformInfoProvider _provider = new();

    [Theory]
    [InlineData(Platform.CSharp)]
    [InlineData(Platform.JavaScript)]
    [InlineData(Platform.Node)]
    [InlineData(Platform.Python)]
    [InlineData(Platform.Java)]
    [InlineData(Platform.Ruby)]
    [InlineData(Platform.PHP)]
    [InlineData(Platform.Go)]
    [InlineData(Platform.Elixir)]
    [InlineData(Platform.Perl)]
    [InlineData(Platform.Cocoa)]
    [InlineData(Platform.ObjectiveC)]
    [InlineData(Platform.C)]
    [InlineData(Platform.Native)]
    [InlineData(Platform.Groovy)]
    [InlineData(Platform.Haskell)]
    [InlineData(Platform.ColdFusion)]
    [InlineData(Platform.ActionScript3)]
    [InlineData(Platform.Other)]
    public void GetInfo_AllEnumValues_ReturnsNonNull(Platform platform)
    {
        _provider.GetInfo(platform).Should().NotBeNull();
    }

    [Fact]
    public void GetInfo_CoversAllEnumValues()
    {
        var allPlatforms = Enum.GetValues<Platform>();
        foreach (var platform in allPlatforms)
        {
            _provider.GetInfo(platform).Should().NotBeNull(
                because: $"Platform.{platform} should have a PlatformInfo entry");
        }
    }

    [Fact]
    public void GetInfo_CSharp_HasExpectedDisplayName()
    {
        var info = _provider.GetInfo(Platform.CSharp);
        info!.DisplayName.Should().Be("C# / .NET");
    }

    [Theory]
    [InlineData(Platform.CSharp)]
    [InlineData(Platform.JavaScript)]
    [InlineData(Platform.Python)]
    [InlineData(Platform.Java)]
    public void GetInfo_KnownPlatform_HasAllRequiredFields(Platform platform)
    {
        var info = _provider.GetInfo(platform)!;
        info.DisplayName.Should().NotBeNullOrEmpty();
        info.SdkPackage.Should().NotBeNullOrEmpty();
        info.InstallCommand.Should().NotBeNullOrEmpty();
        info.InitSnippet.Should().NotBeNullOrEmpty();
        info.Language.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetAll_ReturnsAtLeastOnePlatform()
    {
        _provider.GetAll().Should().NotBeEmpty();
    }

    [Fact]
    public void GetAll_AllEntriesHaveUniqueDisplayNames()
    {
        var names = _provider.GetAll().Select(p => p.DisplayName).ToList();
        names.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void FormatSnippet_KnownPlatform_InsertsRealDsn()
    {
        const string dsn = "https://key@example.com/1";
        var snippet = _provider.FormatSnippet(Platform.CSharp, dsn);
        snippet.Should().Contain(dsn);
        snippet.Should().NotContain("{dsn}");
    }

    [Fact]
    public void FormatSnippet_AllPlatforms_ContainDsn()
    {
        const string dsn = "https://key@example.com/1";
        foreach (var platform in Enum.GetValues<Platform>())
        {
            var snippet = _provider.FormatSnippet(platform, dsn);
            snippet.Should().Contain(dsn, because: $"FormatSnippet for {platform} should include the DSN");
        }
    }

    [Fact]
    public void GetInfo_PlatformMatchesKey()
    {
        var info = _provider.GetInfo(Platform.Python)!;
        info.Platform.Should().Be(Platform.Python);
    }
}
