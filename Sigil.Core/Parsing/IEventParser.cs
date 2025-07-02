using Sigil.Core.Parsing.Models;

namespace Sigil.Core.Parsing;

public interface IEventParser
{
    Task<List<SentryEvent>> Parse(Stream envelopeStream);
}