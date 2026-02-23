using System.Diagnostics.CodeAnalysis;

namespace Sigil.Domain.Extensions;

public static class EnumerableExtensions
{
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T>? enumerable)
    {
        return enumerable == null || !enumerable.Any();
    }
    
    public static bool IsEmpty<T>(this IEnumerable<T> enumerable)
    {
        return !enumerable.Any();
    }
}