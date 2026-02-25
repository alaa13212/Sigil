using Sigil.Application.Interfaces;
using Sigil.Domain.Enums;

namespace Sigil.Application.Services;

public class PythonStackFrameCleaner : IStackFrameCleaner
{
    public Platform Platform => Platform.Python;

    public string CleanMethodName(string methodName) => methodName switch
    {
        "<module>"  => "(module)",
        "<lambda>"  => "(lambda)",
        "<listcomp>" or "<dictcomp>" or "<setcomp>" or "<genexpr>" => "(comprehension)",
        _ => methodName
    };
}
