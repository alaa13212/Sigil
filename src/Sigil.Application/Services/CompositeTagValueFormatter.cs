using Sigil.Application.Interfaces;

namespace Sigil.Application.Services;

public class CompositeTagValueFormatter(IEnumerable<IInternalTagValueFormatter> formatters) : ITagValueFormatter
{
    public string Format(string key, string value)
    {
        return formatters.Aggregate(value, (current, formatter) => formatter.Format(key, current));
    }
}