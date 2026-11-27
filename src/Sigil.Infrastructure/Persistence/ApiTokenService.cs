using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.ApiTokens;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class ApiTokenService(SigilDbContext db, IDateTime dateTime) : IApiTokenService
{
    private const string TokenPrefix = "sigil_";
    private const int SecretByteLength = 32;

    public async Task<ApiTokenCreatedResponse> CreateAsync(Guid userId, string name, DateTime? expiresAt = null)
    {
        string plaintext = GenerateToken();

        var token = new ApiToken
        {
            Name = name,
            TokenHash = HashToken(plaintext),
            UserId = userId,
            CreatedAt = dateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        db.ApiTokens.Add(token);
        await db.SaveChangesAsync();

        return new ApiTokenCreatedResponse(token.Id, token.Name, plaintext, token.CreatedAt, token.ExpiresAt);
    }

    public async Task<List<ApiTokenSummary>> ListAsync(Guid userId)
    {
        return await db.ApiTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new ApiTokenSummary(t.Id, t.Name, t.CreatedAt, t.LastUsedAt, t.ExpiresAt, t.RevokedAt))
            .ToListAsync();
    }

    public async Task<bool> RevokeAsync(Guid userId, int tokenId)
    {
        ApiToken? token = await db.ApiTokens
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId);

        if (token is null)
            return false;

        if (token.RevokedAt is null)
        {
            token.RevokedAt = dateTime.UtcNow;
            db.ApiTokens.Update(token);
            await db.SaveChangesAsync();
        }

        return true;
    }

    public async Task<ApiTokenIdentity?> AuthenticateAsync(string presentedToken)
    {
        if (string.IsNullOrWhiteSpace(presentedToken))
            return null;

        byte[] presentedHash = ComputeHash(presentedToken);

        ApiToken? token = await db.ApiTokens.FirstOrDefaultAsync(t => t.TokenHash == Encode(presentedHash));
        if (token is null)
            return null;

        if (!HashesMatch(token.TokenHash, presentedHash))
            return null;

        DateTime now = dateTime.UtcNow;
        if (token.RevokedAt is not null)
            return null;
        if (token.ExpiresAt is not null && token.ExpiresAt <= now)
            return null;

        string? email = await db.Users
            .Where(u => u.Id == token.UserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync();

        if (email is null)
            return null;

        List<string> roles = await db.UserRoles
            .Where(ur => ur.UserId == token.UserId)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .Where(name => name != null)
            .Select(name => name!)
            .ToListAsync();

        await db.ApiTokens
            .Where(t => t.Id == token.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastUsedAt, now));

        return new ApiTokenIdentity(token.UserId, email, roles);
    }

    private static string GenerateToken()
    {
        byte[] secret = RandomNumberGenerator.GetBytes(SecretByteLength);
        return TokenPrefix + Encode(secret);
    }

    private static string HashToken(string token) => Encode(ComputeHash(token));

    private static byte[] ComputeHash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static bool HashesMatch(string storedHash, byte[] presentedHash)
    {
        byte[] stored = Decode(storedHash);
        return stored.Length == presentedHash.Length && CryptographicOperations.FixedTimeEquals(stored, presentedHash);
    }

    private static string Encode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Decode(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded
        };

        try
        {
            return Convert.FromBase64String(padded);
        }
        catch (FormatException)
        {
            return [];
        }
    }
}
