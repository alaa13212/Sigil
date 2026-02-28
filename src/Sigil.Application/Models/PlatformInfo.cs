using Sigil.Domain.Enums;

namespace Sigil.Application.Models;

public record PlatformInfo
{
    public required Platform Platform { get; init; }
    public required string DisplayName { get; init; }
    public required string SdkPackage { get; init; }
    public required string InstallCommand { get; init; }
    public required string InitSnippet { get; init; }
    public required string Language { get; init; }
    public string? DocumentationUrl { get; init; }
    public string? SdkGitHubUrl { get; init; }
}
