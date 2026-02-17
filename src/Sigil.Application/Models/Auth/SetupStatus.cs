namespace Sigil.Application.Models.Auth;

public record SetupStatus(bool IsComplete, int UserCount);

public enum DbConnectionStatus
{
    Connected,
    DatabaseNotFound,
    ConnectionFailed
}

public record DbStatusResponse(
    DbConnectionStatus Status,
    string? Error,
    IReadOnlyList<string> Pending,
    IReadOnlyList<string> Applied);
