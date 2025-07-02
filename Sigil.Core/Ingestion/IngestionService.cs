using Sigil.Core.IssueGrouping;
using Sigil.Core.Parsing;
using Sigil.Core.Parsing.Models;

namespace Sigil.Core.Ingestion;

public class IngestionService(IEventParser eventParser, IFingerprintGenerator fingerprintGenerator) : IIngestionService
{
    public async Task Ingest(string projectId, Stream envelopeStream)
    {
        IEnumerable<SentryEvent> sentryEvents = await eventParser.Parse(envelopeStream);
        // TODO actually ingest when we make Database models
        
        
    }
}