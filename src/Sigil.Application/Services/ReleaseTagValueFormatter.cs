using Sigil.Application.Interfaces;

namespace Sigil.Application.Services;

public class ReleaseTagValueFormatter : IInternalTagValueFormatter
{
    public string Format(string key, string value)
    {
        if (key == "release" && value.Contains('@'))
        {
            return value[(value.IndexOf('@') + 1)..];
        }
        
        return value;
    }
}