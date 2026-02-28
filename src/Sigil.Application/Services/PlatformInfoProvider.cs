using Sigil.Application.Models;
using Sigil.Domain.Enums;

namespace Sigil.Application.Services;

public class PlatformInfoProvider
{
    private static readonly Dictionary<Platform, PlatformInfo> _platforms = new()
    {
        [Platform.CSharp] = new PlatformInfo
        {
            Platform = Platform.CSharp,
            DisplayName = "C# / .NET",
            SdkPackage = "Sentry",
            InstallCommand = "dotnet add package Sentry",
            InitSnippet = """
                SentrySdk.Init(options =>
                {
                    options.Dsn = "{dsn}";
                    options.TracesSampleRate = 1.0;
                });
                """,
            Language = "csharp",
            DocumentationUrl = "https://docs.sentry.io/platforms/dotnet/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-dotnet",
        },
        [Platform.JavaScript] = new PlatformInfo
        {
            Platform = Platform.JavaScript,
            DisplayName = "JavaScript",
            SdkPackage = "@sentry/browser",
            InstallCommand = "npm install @sentry/browser",
            InitSnippet = """
                import * as Sentry from "@sentry/browser";
                Sentry.init({ dsn: "{dsn}" });
                """,
            Language = "javascript",
            DocumentationUrl = "https://docs.sentry.io/platforms/javascript/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-javascript",
        },
        [Platform.Node] = new PlatformInfo
        {
            Platform = Platform.Node,
            DisplayName = "Node.js",
            SdkPackage = "@sentry/node",
            InstallCommand = "npm install @sentry/node",
            InitSnippet = """
                const Sentry = require("@sentry/node");
                Sentry.init({ dsn: "{dsn}" });
                """,
            Language = "javascript",
            DocumentationUrl = "https://docs.sentry.io/platforms/node/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-javascript",
        },
        [Platform.Python] = new PlatformInfo
        {
            Platform = Platform.Python,
            DisplayName = "Python",
            SdkPackage = "sentry-sdk",
            InstallCommand = "pip install sentry-sdk",
            InitSnippet = """
                import sentry_sdk
                sentry_sdk.init(dsn="{dsn}")
                """,
            Language = "python",
            DocumentationUrl = "https://docs.sentry.io/platforms/python/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-python",
        },
        [Platform.Java] = new PlatformInfo
        {
            Platform = Platform.Java,
            DisplayName = "Java",
            SdkPackage = "io.sentry:sentry",
            InstallCommand = "implementation 'io.sentry:sentry:7.0.0'",
            InitSnippet = """
                Sentry.init(options -> {
                    options.setDsn("{dsn}");
                });
                """,
            Language = "java",
            DocumentationUrl = "https://docs.sentry.io/platforms/java/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-java",
        },
        [Platform.Ruby] = new PlatformInfo
        {
            Platform = Platform.Ruby,
            DisplayName = "Ruby",
            SdkPackage = "sentry-ruby",
            InstallCommand = "gem install sentry-ruby",
            InitSnippet = """
                Sentry.init do |config|
                  config.dsn = "{dsn}"
                end
                """,
            Language = "ruby",
            DocumentationUrl = "https://docs.sentry.io/platforms/ruby/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-ruby",
        },
        [Platform.PHP] = new PlatformInfo
        {
            Platform = Platform.PHP,
            DisplayName = "PHP",
            SdkPackage = "sentry/sentry",
            InstallCommand = "composer require sentry/sentry",
            InitSnippet = """
                \Sentry\init(['dsn' => '{dsn}']);
                """,
            Language = "php",
            DocumentationUrl = "https://docs.sentry.io/platforms/php/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-php",
        },
        [Platform.Go] = new PlatformInfo
        {
            Platform = Platform.Go,
            DisplayName = "Go",
            SdkPackage = "github.com/getsentry/sentry-go",
            InstallCommand = "go get github.com/getsentry/sentry-go",
            InitSnippet = """
                sentry.Init(sentry.ClientOptions{
                    Dsn: "{dsn}",
                })
                """,
            Language = "go",
            DocumentationUrl = "https://docs.sentry.io/platforms/go/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-go",
        },
        [Platform.Elixir] = new PlatformInfo
        {
            Platform = Platform.Elixir,
            DisplayName = "Elixir",
            SdkPackage = "sentry",
            InstallCommand = """{:sentry, "~> 10.0"}""",
            InitSnippet = """
                config :sentry,
                  dsn: "{dsn}"
                """,
            Language = "elixir",
            DocumentationUrl = "https://docs.sentry.io/platforms/elixir/",
        },
        [Platform.Perl] = new PlatformInfo
        {
            Platform = Platform.Perl,
            DisplayName = "Perl",
            SdkPackage = "Sentry::SDK",
            InstallCommand = "cpan Sentry::SDK",
            InitSnippet = """
                use Sentry::SDK;
                Sentry::SDK->init({ dsn => "{dsn}" });
                """,
            Language = "perl",
            DocumentationUrl = "https://docs.sentry.io/platforms/perl/",
        },
        [Platform.Cocoa] = new PlatformInfo
        {
            Platform = Platform.Cocoa,
            DisplayName = "Apple (iOS / macOS)",
            SdkPackage = "Sentry",
            InstallCommand = ".package(url: \"https://github.com/getsentry/sentry-cocoa\", from: \"8.0.0\")",
            InitSnippet = """
                import Sentry
                SentrySDK.start { options in
                    options.dsn = "{dsn}"
                }
                """,
            Language = "swift",
            DocumentationUrl = "https://docs.sentry.io/platforms/apple/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-cocoa",
        },
        [Platform.ObjectiveC] = new PlatformInfo
        {
            Platform = Platform.ObjectiveC,
            DisplayName = "Objective-C",
            SdkPackage = "Sentry",
            InstallCommand = "pod 'Sentry', :git => 'https://github.com/getsentry/sentry-cocoa.git'",
            InitSnippet = """
                @import Sentry;
                [SentrySDK startWithConfigureOptions:^(SentryOptions *options) {
                    options.dsn = @"{dsn}";
                }];
                """,
            Language = "objc",
            DocumentationUrl = "https://docs.sentry.io/platforms/apple/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-cocoa",
        },
        [Platform.C] = new PlatformInfo
        {
            Platform = Platform.C,
            DisplayName = "C / C++",
            SdkPackage = "sentry-native",
            InstallCommand = "# See CMake build instructions at https://github.com/getsentry/sentry-native",
            InitSnippet = """
                sentry_options_t *options = sentry_options_new();
                sentry_options_set_dsn(options, "{dsn}");
                sentry_init(options);
                """,
            Language = "c",
            DocumentationUrl = "https://docs.sentry.io/platforms/native/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-native",
        },
        [Platform.Native] = new PlatformInfo
        {
            Platform = Platform.Native,
            DisplayName = "Native (C / C++)",
            SdkPackage = "sentry-native",
            InstallCommand = "# See CMake build instructions at https://github.com/getsentry/sentry-native",
            InitSnippet = """
                sentry_options_t *options = sentry_options_new();
                sentry_options_set_dsn(options, "{dsn}");
                sentry_init(options);
                """,
            Language = "c",
            DocumentationUrl = "https://docs.sentry.io/platforms/native/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-native",
        },
        [Platform.Groovy] = new PlatformInfo
        {
            Platform = Platform.Groovy,
            DisplayName = "Groovy",
            SdkPackage = "io.sentry:sentry",
            InstallCommand = "implementation 'io.sentry:sentry:7.0.0'",
            InitSnippet = """
                Sentry.init { options ->
                    options.dsn = "{dsn}"
                }
                """,
            Language = "groovy",
            DocumentationUrl = "https://docs.sentry.io/platforms/java/",
            SdkGitHubUrl = "https://github.com/getsentry/sentry-java",
        },
        [Platform.Haskell] = new PlatformInfo
        {
            Platform = Platform.Haskell,
            DisplayName = "Haskell",
            SdkPackage = "sentry-haskell",
            InstallCommand = "cabal install sentry-haskell",
            InitSnippet = """
                import Sentry
                main :: IO ()
                main = withSentry defaultSettings { sentry_dsn = "{dsn}" } $ do
                    -- your app code
                """,
            Language = "haskell",
            DocumentationUrl = "https://docs.sentry.io/platforms/",
        },
        [Platform.ColdFusion] = new PlatformInfo
        {
            Platform = Platform.ColdFusion,
            DisplayName = "ColdFusion",
            SdkPackage = "sentry-coldfusion",
            InstallCommand = "# Add sentry-coldfusion to your application",
            InitSnippet = """
                <cfset application.sentry = createObject("component", "sentry").init(
                    dsn="{dsn}"
                )>
                """,
            Language = "markup",
            DocumentationUrl = "https://docs.sentry.io/platforms/",
        },
        [Platform.ActionScript3] = new PlatformInfo
        {
            Platform = Platform.ActionScript3,
            DisplayName = "ActionScript 3",
            SdkPackage = "sentry-as3",
            InstallCommand = "# Add sentry-as3.swc to your project",
            InitSnippet = """
                import io.sentry.Sentry;
                Sentry.init("{dsn}");
                """,
            Language = "actionscript",
            DocumentationUrl = "https://docs.sentry.io/platforms/",
        },
        [Platform.Other] = new PlatformInfo
        {
            Platform = Platform.Other,
            DisplayName = "Other",
            SdkPackage = "Sentry SDK",
            InstallCommand = "# See https://docs.sentry.io/platforms/ for your platform",
            InitSnippet = """
                // Initialize the Sentry SDK for your platform with DSN: {dsn}
                // See https://docs.sentry.io/platforms/ for setup instructions.
                """,
            Language = "plaintext",
            DocumentationUrl = "https://docs.sentry.io/platforms/",
        },
    };

    public PlatformInfo? GetInfo(Platform platform)
        => _platforms.GetValueOrDefault(platform);

    public IReadOnlyList<PlatformInfo> GetAll()
        => _platforms.Values.ToList();

    public string FormatSnippet(Platform platform, string dsn)
    {
        var info = GetInfo(platform);
        return info?.InitSnippet.Replace("{dsn}", dsn) ?? $"// Configure your SDK with DSN: {dsn}";
    }
}
