using System.Text.RegularExpressions;
using Sigil.Application.Interfaces;
using Sigil.Domain.Enums;

namespace Sigil.Application.Services;

public partial class JavaStackFrameCleaner : IStackFrameCleaner
{
    public Platform Platform => Platform.Java;

    public string CleanMethodName(string methodName)
    {
        // Access bridge: access$100 → (bridge)
        if (AccessBridgeRegex().IsMatch(methodName))
            return "(bridge)";

        // Lambda: lambda$$0 or lambda$method$0 → (lambda)
        if (LambdaRegex().IsMatch(methodName))
            return "(lambda)";

        return methodName;
    }

    [GeneratedRegex(@"^access\$\d+$")]
    private static partial Regex AccessBridgeRegex();

    [GeneratedRegex(@"lambda\$")]
    private static partial Regex LambdaRegex();
}
