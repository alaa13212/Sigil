using Sigil.Application.Interfaces;
using Sigil.Domain.Enums;

namespace Sigil.Application.Services;

public class StackFrameCleanerService(IEnumerable<IStackFrameCleaner> cleaners)
{
    private readonly Dictionary<Platform, IStackFrameCleaner> _cleaners =
        cleaners.ToDictionary(c => c.Platform);

    public string CleanMethodName(Platform platform, string? methodName)
    {
        if (string.IsNullOrEmpty(methodName)) return "(unknown)";
        return _cleaners.TryGetValue(platform, out var cleaner)
            ? cleaner.CleanMethodName(methodName)
            : methodName;
    }
}
