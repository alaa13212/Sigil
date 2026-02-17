namespace Sigil.Application.Models.Auth;

public record LoginRequest(string Email, string Password, bool RememberMe = false);
