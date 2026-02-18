namespace Sigil.Application.Models.Auth;

public record ActivateRequest(string Email, string Token, string Password);
