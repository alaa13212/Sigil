using Microsoft.AspNetCore.Mvc;
using Sigil.Core.Ingestion;

namespace Sigil.Server.API;

[ApiController]
public class IngestionController(IIngestionService ingestionService) : ControllerBase
{
    [HttpPost("api/{projectId}/envelope")]
    public async Task<IActionResult> IngestEnvelope(string projectId)
    {
        await ingestionService.Ingest(projectId, Request.Body);
        return Ok();
    }
}
