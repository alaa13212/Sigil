namespace Sigil.Domain.Enums;

public enum Platform
{
    Other = 0,
    ActionScript3,
    C,
    ColdFusion,
    Cocoa,
    CSharp,
    Elixir,
    Haskell,
    Go,
    Groovy,
    Java,
    JavaScript,
    Native,
    Node,
    ObjectiveC,
    Perl,
    PHP,
    Python,
    Ruby
}

public static class PlatformHelper
{
    public static Platform Parse(string platform) => platform.ToLowerInvariant() switch
    {
        "as3" => Platform.ActionScript3,
        "c" => Platform.C,
        "cfml" => Platform.ColdFusion,
        "cocoa" => Platform.Cocoa,
        "csharp" => Platform.CSharp,
        "elixir" => Platform.Elixir,
        "haskell" => Platform.Haskell,
        "go" => Platform.Go,
        "groovy" => Platform.Groovy,
        "java" => Platform.Java,
        "javascript" => Platform.JavaScript,
        "native" => Platform.Native,
        "node" => Platform.Node,
        "objc" => Platform.ObjectiveC,
        "perl" => Platform.Perl,
        "php" => Platform.PHP,
        "python" => Platform.Python,
        "ruby" => Platform.Ruby,
        "other" => Platform.Other,
        _ => Platform.Other
    };

    public static string ToStringValue(Platform platform) => platform switch
    {
        Platform.ActionScript3 => "as3",
        Platform.C => "c",
        Platform.ColdFusion => "cfml",
        Platform.Cocoa => "cocoa",
        Platform.CSharp => "csharp",
        Platform.Elixir => "elixir",
        Platform.Haskell => "haskell",
        Platform.Go => "go",
        Platform.Groovy => "groovy",
        Platform.Java => "java",
        Platform.JavaScript => "javascript",
        Platform.Native => "native",
        Platform.Node => "node",
        Platform.ObjectiveC => "objc",
        Platform.Perl => "perl",
        Platform.PHP => "php",
        Platform.Python => "python",
        Platform.Ruby => "ruby",
        Platform.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(platform), $"Unhandled platform enum: {platform}")
    };
}