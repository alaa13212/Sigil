using Microsoft.AspNetCore.DataProtection;
using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class TokenEncryptionServiceTests
{
    private static TokenEncryptionService CreateService()
    {
        var provider = new EphemeralDataProtectionProvider();
        return new TokenEncryptionService(provider);
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip_ReturnsOriginal()
    {
        var service = CreateService();
        const string original = "ghp_supersecretaccesstoken123";

        var encrypted = service.Encrypt(original);
        var decrypted = service.Decrypt(encrypted);

        decrypted.Should().Be(original);
    }

    [Fact]
    public void Encrypt_ReturnsDifferentValue()
    {
        var service = CreateService();
        const string original = "glpat-my-gitlab-token";

        var encrypted = service.Encrypt(original);

        encrypted.Should().NotBe(original);
        encrypted.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Encrypt_SameInput_ProducesDifferentCiphertextsEachTime()
    {
        var service = CreateService();
        const string original = "my-token-value";

        var encrypted1 = service.Encrypt(original);
        var encrypted2 = service.Encrypt(original);

        // Data Protection produces non-deterministic ciphertext
        encrypted1.Should().NotBe(encrypted2);
    }

    [Fact]
    public void Decrypt_WrongKey_Throws()
    {
        var serviceA = new TokenEncryptionService(new EphemeralDataProtectionProvider());
        var serviceB = new TokenEncryptionService(new EphemeralDataProtectionProvider());
        const string original = "secret-token";

        var encrypted = serviceA.Encrypt(original);

        // Decrypting with a different provider's key should throw a CryptographicException
        var act = () => serviceB.Decrypt(encrypted);

        act.Should().Throw<Exception>("decrypting with a mismatched key should fail");
    }
}
