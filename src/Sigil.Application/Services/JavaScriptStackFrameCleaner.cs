using Sigil.Application.Interfaces;
using Sigil.Domain.Enums;

namespace Sigil.Application.Services;

public class JavaScriptStackFrameCleaner : IStackFrameCleaner
{
    public Platform Platform => Platform.JavaScript;

    public string CleanMethodName(string methodName)
    {
        if (methodName is "<anonymous>" or "anonymous")
            return "(anonymous)";

        // Webpack internal: webpack:///./src/... → (webpack)
        if (methodName.StartsWith("webpack:///", StringComparison.Ordinal))
            return "(webpack module)";

        return methodName;
    }
}
