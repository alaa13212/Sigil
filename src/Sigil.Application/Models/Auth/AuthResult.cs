namespace Sigil.Application.Models.Auth;

public class AuthResult
{
    public bool Succeeded { get; init; }
    public UserInfo? User { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static AuthResult Success(UserInfo user) => new() { Succeeded = true, User = user };

    public static AuthResult Failure(params string[] errors) => new() { Succeeded = false, Errors = errors };

    public static AuthResult Failure(IEnumerable<string> errors) => new() { Succeeded = false, Errors = errors.ToList() };
}
