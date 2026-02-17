namespace Sigil.Application.Models.Auth;

public record SetupStatus(bool IsComplete, int UserCount);

public record DbStatusResponse(bool CanConnect, string? Error, IReadOnlyList<string> Pending, IReadOnlyList<string> Applied);
