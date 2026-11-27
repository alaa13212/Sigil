using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Saml;

public record SamlGroupMappingRequest(
    string GroupName,
    string? TeamName = null,
    TeamRole Role = TeamRole.Member,
    bool GrantsGlobalAdmin = false);

public record SamlConnectionRequest(
    string Name,
    string EntityId,
    string? DisplayName = null,
    string? MetadataUrl = null,
    bool IsEnabled = true,
    string? EmailClaimType = null,
    string? NameClaimType = null,
    string? GroupsClaimType = null,
    string? DefaultTeamName = null,
    List<SamlGroupMappingRequest>? GroupMappings = null);

public record SamlGroupMappingResponse(
    Guid Id,
    string GroupName,
    string? TeamName,
    TeamRole Role,
    bool GrantsGlobalAdmin);

public record SamlConnectionResponse(
    Guid Id,
    string Name,
    string EntityId,
    string? DisplayName,
    string? MetadataUrl,
    bool HasMetadata,
    DateTime? MetadataFetchedAt,
    string? MetadataError,
    bool IsEnabled,
    string EmailClaimType,
    string NameClaimType,
    string GroupsClaimType,
    string? DefaultTeamName,
    List<SamlGroupMappingResponse> GroupMappings,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record SamlLoginOptionResponse(Guid Id, string Name, string? DisplayName);

public record SamlConnectionRuntimeConfig(Guid Id, string EntityId, string? MetadataXml);

public record SamlEndpointsResponse(string EntityId, string MetadataUrl, string AcsUrl);

public static class SamlDefaults
{
    public const string ModulePath = "/saml/sp";
    public const string LoginPath = "/saml/login";
    public const string CompletePath = "/saml/complete";
    public const string DefaultEntityId = "urn:sigil:saml";
}

public record SamlPendingUserResponse(
    Guid UserId,
    string Email,
    string? DisplayName,
    string? ConnectionName,
    DateTime CreatedAt);

public record SamlAssertion(IReadOnlyDictionary<string, IReadOnlyList<string>> Claims);

public enum SamlLoginStatus
{
    Approved,
    Pending,
    Rejected,
    Failed
}

public record SamlLoginResult(SamlLoginStatus Status, Guid? UserId, string? Message);
