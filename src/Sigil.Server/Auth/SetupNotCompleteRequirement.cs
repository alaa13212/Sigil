using Microsoft.AspNetCore.Authorization;
using Sigil.Application.Interfaces;

namespace Sigil.Server.Auth;

public class SetupNotCompleteRequirement : IAuthorizationRequirement;

public class SetupNotCompleteHandler(ISetupService setupService) : AuthorizationHandler<SetupNotCompleteRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SetupNotCompleteRequirement requirement)
    {
        var status = await setupService.GetSetupStatusAsync();
        if (!status.IsComplete)
            context.Succeed(requirement);
    }
}
