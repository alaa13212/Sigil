namespace Sigil.Application.Models.Auth;

public record RegisterRequest(string Email, string Password, string DisplayName);
