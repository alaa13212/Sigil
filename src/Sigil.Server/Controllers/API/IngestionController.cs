using Microsoft.AspNetCore.Mvc;
using Sigil.Application.Interfaces;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;
using Sigil.Server.Controllers.Filters;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
public class IngestionController(IEventIngestionWorker ingestionWorker, IRateLimiter rateLimiter, IProjectConfigService projectConfigService) : SigilController
{
    [EnvelopeAuthorize]
    [HttpPost("api/{projectId:int}/envelope")]
    public async Task<IActionResult> IngestEnvelope(int projectId)
    {
        int? projectLimit = projectConfigService.RateLimitMaxEventsPerWindow(projectId);

        if (!rateLimiter.TryAcquire(projectId, projectLimit))
            return TooManyRequests(TimeSpan.FromSeconds(60));

        string rawEnvelope = await Request.Body.ReadAsStringAsync();
        if(ingestionWorker.TryEnqueue(new IngestionJobItem(projectId, rawEnvelope, DateTime.UtcNow)))
            return Accepted();

        return TooManyRequests(TimeSpan.FromSeconds(30));
    }
}
