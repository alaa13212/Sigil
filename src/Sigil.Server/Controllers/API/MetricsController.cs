using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.Metrics;
using Sigil.Application.Services;

namespace Sigil.Server.Controllers.API;

/// <summary>Exposes Prometheus metrics at GET /metrics. Gated by a dedicated token or CIDR allow-list.</summary>
[ApiController]
[AllowAnonymous]
public class MetricsController(
    IMetricsQueryService queryService,
    IAppConfigService appConfig,
    ISigilMetrics metrics) : ControllerBase
{
    [HttpGet("/metrics")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get()
    {
        if (!IsAuthorized(out MetricsAccess denial))
            return StatusCode(MetricsAccessControl.ToStatusCode(denial), DeniedBody(denial));

        MetricsSnapshot snapshot = await queryService.GetSnapshotAsync(HttpContext.RequestAborted);

        // Re-read the counters so a scrape taken after the query still reflects emits
        // that landed while the database aggregates were running.
        SigilMetricsSnapshot counters = metrics.GetSnapshot();
        snapshot = snapshot with
        {
            EventsIngested = counters.EventsIngested,
            EventsDropped = counters.EventsDropped,
            LastIngestionUtc = counters.LastIngestionUtc
        };

        return Content(PrometheusExpositionWriter.Write(snapshot), PrometheusExpositionWriter.ContentType);
    }

    private bool IsAuthorized(out MetricsAccess access)
    {
        string? authorization = Request.Headers.TryGetValue(MetricsAccessControl.AuthorizationHeader, out var header)
            ? header.ToString()
            : null;

        string? queryToken = Request.Query.TryGetValue(MetricsAccessControl.TokenQueryParameter, out var query)
            ? query.ToString()
            : null;

        string? presented = MetricsAccessControl.ExtractToken(authorization, queryToken);
        string? remoteAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        access = MetricsAccessControl.Evaluate(
            appConfig.MetricsToken,
            appConfig.MetricsAllowedCidrs,
            presented,
            remoteAddress);

        return access == MetricsAccess.Allowed;
    }

    private static string DeniedBody(MetricsAccess access) => access switch
    {
        MetricsAccess.NotConfigured =>
            "Metrics endpoint is not configured. Set metrics_token or metrics_allowed_cidrs in AppConfig.",
        MetricsAccess.MissingCredentials =>
            "Metrics endpoint requires a token. Send 'Authorization: Bearer <token>' or '?token=<token>'.",
        _ => "Metrics token is not valid."
    };
}
