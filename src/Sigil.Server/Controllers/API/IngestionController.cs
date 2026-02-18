using Microsoft.AspNetCore.Mvc;
using Sigil.Domain.Extensions;
using Sigil.Domain.Ingestion;
using Sigil.Domain.Interfaces;
using Sigil.Server.Controllers.Filters;
using Sigil.Server.Framework;

namespace Sigil.Server.Controllers.API;

[ApiController]
public class IngestionController(IEventIngestionWorker ingestionWorker) : SigilController
{
    [EnvelopeAuthorize]
    [HttpPost("api/{projectId:int}/envelope")]
    public async Task<IActionResult> IngestEnvelope(int projectId)
    {
        string rawEnvelope = await Request.Body.ReadAsStringAsync();
        if(ingestionWorker.TryEnqueue(new IngestionJobItem(projectId, rawEnvelope, DateTime.UtcNow)))
            return Accepted();
        
        return TooManyRequests(TimeSpan.FromSeconds(30));
    }
}
