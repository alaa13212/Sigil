using Sigil.Application.Models.Auth;

namespace Sigil.Application.Interfaces;

public interface ICurrentUserService
{
    Task<UserInfo?> GetCurrentUserAsync();
}
