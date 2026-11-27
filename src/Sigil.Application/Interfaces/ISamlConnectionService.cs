using Sigil.Application.Models.Saml;

namespace Sigil.Application.Interfaces;

public interface ISamlConnectionService
{
    Task<List<SamlConnectionResponse>> GetConnectionsAsync();
    Task<List<SamlLoginOptionResponse>> GetEnabledConnectionsAsync();
    Task<SamlEndpointsResponse> GetEndpointsAsync();
    Task<SamlConnectionResponse?> GetConnectionAsync(Guid id);
    Task<SamlConnectionResponse> CreateConnectionAsync(SamlConnectionRequest request);
    Task<SamlConnectionResponse?> UpdateConnectionAsync(Guid id, SamlConnectionRequest request);
    Task<bool> DeleteConnectionAsync(Guid id);
    Task<bool> SetMetadataAsync(Guid id, string? metadataXml, DateTime? fetchedAt, string? error);
    Task<bool> RefreshMetadataAsync(Guid id);
    Task<List<SamlPendingUserResponse>> GetPendingUsersAsync();
    Task<bool> ApproveUserAsync(Guid userId);
    Task<bool> RejectUserAsync(Guid userId);
}
