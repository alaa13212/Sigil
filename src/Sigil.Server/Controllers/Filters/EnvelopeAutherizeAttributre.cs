using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Extensions;

namespace Sigil.Server.Controllers.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class EnvelopeAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        int projectId = Convert.ToInt32(context.RouteData.Values["projectId"] ?? "0");
        if (projectId < 1)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        IProjectService projectService = context.HttpContext.RequestServices.GetRequiredService<IProjectService>();
        Project? project = await projectService.GetProjectByIdAsync(projectId);
        if (project == null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        string? apiKey = ExtractApiKey(context.HttpContext.Request);
        if (apiKey.IsNullOrEmpty() || apiKey != project.ApiKey)
            context.Result = new UnauthorizedResult();
    }
    
    private string? ExtractApiKey(HttpRequest request)
    {
        // Check X-Sentry-Auth header (Sentry SDK format)
        if (request.Headers.TryGetValue("X-Sentry-Auth", out StringValues sentryAuth))
        {
            // Format: "Sentry sentry_key={key}, sentry_version=7"
            var keyMatch = Regex.Match(sentryAuth.ToString(), @"sentry_key=([^,\s]+)");
            if (keyMatch.Success)
                return keyMatch.Groups[1].Value;
        }
        
        return null;
    }
}