using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Auth;
using Sigil.Domain;
using Sigil.Domain.Entities;
using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Persistence;

internal class PasskeyService(
    IAppConfigService appConfig,
    SigilDbContext db,
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    PasskeyChallengeStore challengeStore) : IPasskeyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private Fido2 CreateFido2()
    {
        var hostUrl = appConfig.HostUrl ?? "http://localhost";
        var uri = new Uri(hostUrl);
        var origin = $"{uri.Scheme}://{uri.Authority}";
        return new Fido2(new Fido2Configuration
        {
            ServerDomain = uri.Host,
            ServerName = "Sigil",
            Origins = new HashSet<string> { origin }
        });
    }

    public async Task<PasskeyRegistrationOptions> GetRegistrationOptionsAsync(Guid userId)
    {
        var fido2 = CreateFido2();

        var user = await userManager.FindByIdAsync(userId.ToString())
                   ?? throw new InvalidOperationException("User not found.");

        var existingKeys = await db.Passkeys
            .Where(p => p.UserId == userId)
            .Select(p => new PublicKeyCredentialDescriptor(p.CredentialId))
            .ToListAsync();

        var fidoUser = new Fido2User
        {
            Id = userId.ToByteArray(),
            Name = user.Email!,
            DisplayName = user.DisplayName ?? user.Email!
        };

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fidoUser,
            ExcludeCredentials = existingKeys,
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Preferred
            },
            AttestationPreference = AttestationConveyancePreference.None
        });

        var optionsJson = JsonSerializer.Serialize(options, JsonOptions);
        challengeStore.Store($"reg:{userId}", optionsJson);

        return new PasskeyRegistrationOptions { OptionsJson = optionsJson };
    }

    public async Task<AuthResult> CompleteRegistrationAsync(Guid userId, PasskeyRegistrationResponse response)
    {
        var fido2 = CreateFido2();

        var storedJson = challengeStore.Get($"reg:{userId}");
        if (storedJson is null)
            return AuthResult.Failure("Registration challenge expired or not found.");

        var originalOptions = JsonSerializer.Deserialize<CredentialCreateOptions>(storedJson, JsonOptions)!;

        AuthenticatorAttestationRawResponse attestationResponse;
        try
        {
            attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(
                response.AttestationResponseJson, JsonOptions)!;
        }
        catch
        {
            return AuthResult.Failure("Invalid attestation response.");
        }

        RegisteredPublicKeyCredential credential;
        try
        {
            credential = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponse,
                OriginalOptions = originalOptions,
                IsCredentialIdUniqueToUserCallback = async (args, _) =>
                {
                    var exists = await db.Passkeys.AnyAsync(p => p.CredentialId == args.CredentialId);
                    return !exists;
                }
            });
        }
        catch (Fido2VerificationException ex)
        {
            return AuthResult.Failure($"Attestation verification failed: {ex.Message}");
        }

        var passkey = new UserPasskey
        {
            UserId = userId,
            CredentialId = credential.Id,
            PublicKey = credential.PublicKey,
            SignatureCounter = credential.SignCount,
            CredentialType = credential.Type.ToString(),
            AaGuid = credential.AaGuid,
            DisplayName = response.DisplayName,
            CreatedAt = DateTime.UtcNow,
            IsDiscoverable = true
        };

        db.Passkeys.Add(passkey);
        await db.SaveChangesAsync();

        var user = await userManager.FindByIdAsync(userId.ToString());
        var roles = user is not null ? await userManager.GetRolesAsync(user) : (IList<string>)[];
        return AuthResult.Success(new UserInfo(userId, user?.Email ?? "", user?.DisplayName, user?.CreatedAt ?? DateTime.UtcNow, user?.LastLogin, roles.ToList(), user?.EmailConfirmed ?? false));
    }

    public async Task<PasskeyAssertionOptions> GetAssertionOptionsAsync()
    {
        var fido2 = CreateFido2();

        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Preferred
        });

        var challengeId = Guid.NewGuid().ToString("N");
        var optionsJson = JsonSerializer.Serialize(options, JsonOptions);
        challengeStore.Store($"auth:{challengeId}", optionsJson);

        return new PasskeyAssertionOptions
        {
            OptionsJson = optionsJson,
            ChallengeId = challengeId
        };
    }

    public async Task<AuthResult> CompleteAssertionAsync(PasskeyAssertionResponse response)
    {
        var fido2 = CreateFido2();

        var storedJson = challengeStore.Get($"auth:{response.ChallengeId}");
        if (storedJson is null)
            return AuthResult.Failure("Authentication challenge expired or not found.");

        var originalOptions = JsonSerializer.Deserialize<AssertionOptions>(storedJson, JsonOptions)!;

        AuthenticatorAssertionRawResponse assertionResponse;
        try
        {
            assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
                response.AssertionResponseJson, JsonOptions)!;
        }
        catch
        {
            return AuthResult.Failure("Invalid assertion response.");
        }

        var passkey = await db.Passkeys
            .FirstOrDefaultAsync(p => p.CredentialId == assertionResponse.RawId);

        if (passkey is null)
            return AuthResult.Failure("Passkey not recognized.");

        VerifyAssertionResult result;
        try
        {
            result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = originalOptions,
                StoredPublicKey = passkey.PublicKey,
                StoredSignatureCounter = passkey.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = async (args, _) =>
                {
                    var ownerPasskey = await db.Passkeys
                        .FirstOrDefaultAsync(p => p.CredentialId == args.CredentialId);
                    if (ownerPasskey is null) return false;
                    return ownerPasskey.UserId.ToByteArray().SequenceEqual(args.UserHandle);
                }
            });
        }
        catch (Fido2VerificationException ex)
        {
            return AuthResult.Failure($"Assertion verification failed: {ex.Message}");
        }

        passkey.SignatureCounter = result.SignCount;
        passkey.LastUsedAt = DateTime.UtcNow;
        db.Passkeys.Update(passkey);
        await db.SaveChangesAsync();

        var user = await userManager.FindByIdAsync(passkey.UserId.ToString());
        if (user is null)
            return AuthResult.Failure("User account not found.");

        user.LastLogin = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        await signInManager.SignInAsync(user, isPersistent: false);

        var roles = await userManager.GetRolesAsync(user);
        return AuthResult.Success(new UserInfo(user.Id, user.Email!, user.DisplayName, user.CreatedAt, user.LastLogin, roles.ToList(), user.EmailConfirmed));
    }

    public async Task<List<PasskeyInfo>> GetPasskeysAsync(Guid userId)
    {
        return await db.Passkeys
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new PasskeyInfo(p.Id, p.DisplayName, p.CreatedAt, p.LastUsedAt))
            .ToListAsync();
    }

    public async Task<bool> DeletePasskeyAsync(Guid userId, int passkeyId)
    {
        var passkey = await db.Passkeys
            .FirstOrDefaultAsync(p => p.Id == passkeyId && p.UserId == userId);

        if (passkey is null)
            return false;

        db.Passkeys.Remove(passkey);
        await db.SaveChangesAsync();
        return true;
    }
}
