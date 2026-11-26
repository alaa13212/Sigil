using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sigil.Application.Interfaces;
using Sigil.Domain.Cors;

namespace Sigil.Server.Middleware;

public sealed class ProjectCorsMiddleware(RequestDelegate next, IProjectConfigService projectConfigService)
{
    private const string AllowOriginHeader = "Access-Control-Allow-Origin";
    private const string AllowMethodsHeader = "Access-Control-Allow-Methods";
    private const string AllowHeadersHeader = "Access-Control-Allow-Headers";
    private const string MaxAgeHeader = "Access-Control-Max-Age";
    private const string RequestMethodHeader = "Access-Control-Request-Method";
    private const string RequestHeadersHeader = "Access-Control-Request-Headers";

    private const string AllowedMethods = "POST, OPTIONS";
    private const string MaxAgeSeconds = "600";

    private static readonly string[] DefaultAllowedHeaders =
    [
        "Content-Type",
        "X-Sentry-Auth",
        "X-Requested-With"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!TryGetProjectId(context.Request.Path, out int projectId))
        {
            await next(context);
            return;
        }

        string? origin = context.Request.Headers.Origin.ToString();

        if (string.IsNullOrEmpty(origin))
        {
            await next(context);
            return;
        }

        string? normalized = CorsOrigins.Normalize(origin);

        if (normalized is not null && IsSameOrigin(context.Request, normalized))
        {
            await next(context);
            return;
        }

        IReadOnlyList<string> allowed = projectConfigService.AllowedCorsOrigins(projectId);

        if (!CorsOrigins.IsMatch(allowed, normalized))
        {
            await RejectAsync(context);
            return;
        }

        context.Response.Headers[AllowOriginHeader] = normalized;
        AppendVaryOrigin(context.Response);

        if (HttpMethods.IsOptions(context.Request.Method) && context.Request.Headers.ContainsKey(RequestMethodHeader))
        {
            context.Response.Headers[AllowMethodsHeader] = AllowedMethods;
            context.Response.Headers[AllowHeadersHeader] = ResolveAllowedHeaders(context.Request);
            context.Response.Headers[MaxAgeHeader] = MaxAgeSeconds;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await next(context);
    }

    private static bool TryGetProjectId(PathString path, out int projectId)
    {
        projectId = 0;

        string[] segments = (path.Value ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 3)
            return false;

        if (!segments[0].Equals("api", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!segments[2].Equals("envelope", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(segments[1], out projectId);
    }

    private static bool IsSameOrigin(HttpRequest request, string normalizedOrigin)
    {
        string? self = CorsOrigins.Normalize($"{request.Scheme}://{request.Host}");
        return self is not null && string.Equals(self, normalizedOrigin, StringComparison.Ordinal);
    }

    private static string ResolveAllowedHeaders(HttpRequest request)
    {
        string requested = request.Headers[RequestHeadersHeader].ToString();
        return string.IsNullOrWhiteSpace(requested)
            ? string.Join(", ", DefaultAllowedHeaders)
            : requested;
    }

    private static void AppendVaryOrigin(HttpResponse response)
    {
        foreach (string? value in response.Headers.Vary)
        {
            if (value is not null && value.Contains("Origin", StringComparison.OrdinalIgnoreCase))
                return;
        }

        response.Headers.Append("Vary", "Origin");
    }

    private static async Task RejectAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error = "Origin is not allowed for this project."
        }));
    }
}
