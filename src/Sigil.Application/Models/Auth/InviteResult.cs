namespace Sigil.Application.Models.Auth;

public class InviteResult
{
    public bool Succeeded { get; init; }
    public Guid UserId { get; init; }
    public string? Email { get; init; }
    public string? ActivationUri { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static InviteResult Success(Guid userId, string email, string activationUri) =>
        new() { Succeeded = true, UserId = userId, Email = email, ActivationUri = activationUri };

    public static InviteResult Failure(params string[] errors) =>
        new() { Succeeded = false, Errors = errors };

    public static InviteResult Failure(IEnumerable<string> errors) =>
        new() { Succeeded = false, Errors = errors.ToList() };
}
