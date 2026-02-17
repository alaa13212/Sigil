using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Auth;

public record SetupRequest(
    string AdminEmail,
    string AdminPassword,
    string AdminDisplayName,
    string TeamName,
    string ProjectName,
    Platform ProjectPlatform,
    string? HostUrl = null);
