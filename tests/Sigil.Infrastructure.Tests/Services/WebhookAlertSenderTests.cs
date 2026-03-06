using System.Security.Cryptography;
using System.Text;
using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Tests.Services;

public class WebhookAlertSenderTests
{
    [Fact]
    public void ComputeHmacSha256_Deterministic()
    {
        var result1 = WebhookAlertSender.ComputeHmacSha256("payload", "secret");
        var result2 = WebhookAlertSender.ComputeHmacSha256("payload", "secret");

        result1.Should().Be(result2);
    }

    [Fact]
    public void ComputeHmacSha256_DifferentPayloads_DifferentHashes()
    {
        var result1 = WebhookAlertSender.ComputeHmacSha256("payload1", "secret");
        var result2 = WebhookAlertSender.ComputeHmacSha256("payload2", "secret");

        result1.Should().NotBe(result2);
    }

    [Fact]
    public void ComputeHmacSha256_DifferentSecrets_DifferentHashes()
    {
        var result1 = WebhookAlertSender.ComputeHmacSha256("payload", "secret1");
        var result2 = WebhookAlertSender.ComputeHmacSha256("payload", "secret2");

        result1.Should().NotBe(result2);
    }

    [Fact]
    public void ComputeHmacSha256_ReturnsLowercaseHex()
    {
        var result = WebhookAlertSender.ComputeHmacSha256("test", "key");

        result.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void ComputeHmacSha256_MatchesKnownValue()
    {
        // Independently compute HMAC-SHA256
        var key = "mysecret"u8.ToArray();
        var data = "mypayload"u8.ToArray();
        var expected = Convert.ToHexString(HMACSHA256.HashData(key, data)).ToLower();

        var result = WebhookAlertSender.ComputeHmacSha256("mypayload", "mysecret");

        result.Should().Be(expected);
    }
}
