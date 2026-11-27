using Sigil.Application.Models.ApiTokens;

namespace Sigil.Application.Interfaces;

/// <summary>Manages read-only API tokens and resolves a presented token to its owner.</summary>
public interface IApiTokenService
{
    /// <summary>Creates a token and returns its plaintext secret. The secret is not recoverable afterwards.</summary>
    Task<ApiTokenCreatedResponse> CreateAsync(Guid userId, string name, DateTime? expiresAt = null);

    /// <summary>Lists the tokens owned by a user without their secrets or hashes.</summary>
    Task<List<ApiTokenSummary>> ListAsync(Guid userId);

    /// <summary>Revokes a token owned by a user.</summary>
    Task<bool> RevokeAsync(Guid userId, int tokenId);

    /// <summary>Resolves a presented token to its owner, rejecting unknown, revoked and expired tokens.</summary>
    Task<ApiTokenIdentity?> AuthenticateAsync(string presentedToken);
}
