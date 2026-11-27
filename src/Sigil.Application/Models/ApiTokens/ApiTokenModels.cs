namespace Sigil.Application.Models.ApiTokens;

/// <summary>A newly created API token. The plaintext secret is present only in this response.</summary>
public record ApiTokenCreatedResponse(int Id, string Name, string Token, DateTime CreatedAt, DateTime? ExpiresAt);

/// <summary>Metadata of an API token. Never carries the token secret or its hash.</summary>
public record ApiTokenSummary(int Id, string Name, DateTime CreatedAt, DateTime? LastUsedAt, DateTime? ExpiresAt, DateTime? RevokedAt);

/// <summary>The identity an API token resolves to.</summary>
public record ApiTokenIdentity(Guid UserId, string Email, IReadOnlyList<string> Roles);

/// <summary>Request to create an API token.</summary>
public record CreateApiTokenRequest(string Name, DateTime? ExpiresAt = null);
