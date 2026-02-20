namespace Sigil.Application.Interfaces;

public interface ITagValueFormatter
{
    string Format(string key, string value);
}

public interface IInternalTagValueFormatter : ITagValueFormatter;