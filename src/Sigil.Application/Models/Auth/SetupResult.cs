namespace Sigil.Application.Models.Auth;

public class SetupResult
{
    public bool Succeeded { get; init; }
    public UserInfo? Admin { get; init; }
    public string? ProjectApiKey { get; init; }
    public int? ProjectId { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static SetupResult Success(UserInfo admin, string projectApiKey, int projectId) =>
        new() { Succeeded = true, Admin = admin, ProjectApiKey = projectApiKey, ProjectId = projectId };

    public static SetupResult Failure(params string[] errors) =>
        new() { Succeeded = false, Errors = errors };

    public static SetupResult Failure(IEnumerable<string> errors) =>
        new() { Succeeded = false, Errors = errors.ToList() };
}
