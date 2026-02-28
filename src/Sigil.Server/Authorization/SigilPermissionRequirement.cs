using Microsoft.AspNetCore.Authorization;

namespace Sigil.Server.Authorization;

public class SigilPermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
