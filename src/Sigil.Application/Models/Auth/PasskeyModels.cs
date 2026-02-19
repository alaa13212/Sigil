namespace Sigil.Application.Models.Auth;

public class PasskeyRegistrationOptions
{
    public required string OptionsJson { get; set; }
}

public class PasskeyRegistrationResponse
{
    public required string AttestationResponseJson { get; set; }
    public required string DisplayName { get; set; }
}

public class PasskeyAssertionOptions
{
    public required string OptionsJson { get; set; }
    public required string ChallengeId { get; set; }
}

public class PasskeyAssertionResponse
{
    public required string AssertionResponseJson { get; set; }
    public required string ChallengeId { get; set; }
}

public record PasskeyInfo(int Id, string DisplayName, DateTime CreatedAt, DateTime? LastUsedAt);
