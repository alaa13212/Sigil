namespace Sigil.Application.Models.Auth;

public record UserInfo(Guid Id, string Email, string? DisplayName, DateTime CreatedAt, DateTime? LastLogin, IReadOnlyList<string> Roles, bool IsActivated = true, string? ActivationUri = null);
