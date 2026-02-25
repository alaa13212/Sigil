using System.Text.RegularExpressions;
using Sigil.Application.Interfaces;
using Sigil.Domain.Enums;

namespace Sigil.Application.Services;

public partial class CSharpStackFrameCleaner : IStackFrameCleaner
{
    public Platform Platform => Platform.CSharp;

    public string CleanMethodName(string methodName)
    {
        // Async state machine: <MethodName>d__5.MoveNext → MethodName (async)
        var asyncMatch = AsyncStateMachineRegex().Match(methodName);
        if (asyncMatch.Success)
            return $"{asyncMatch.Groups[1].Value} (async)";

        // Compiler-generated lambda: <>b__0_0 → (lambda)
        if (LambdaRegex().IsMatch(methodName))
            return "(lambda)";

        // Closure class: <>c__DisplayClass0_0 → (closure)
        if (ClosureRegex().IsMatch(methodName))
            return "(closure)";

        // Remove generic arity: List`1 → List, Dictionary`2 → Dictionary
        var cleaned = GenericArityRegex().Replace(methodName, "");

        return cleaned;
    }

    [GeneratedRegex(@"<(\w+)>d__\d+\.MoveNext")]
    private static partial Regex AsyncStateMachineRegex();

    [GeneratedRegex(@"<>b__\d+_\d+|<[^>]+>b__\d+")]
    private static partial Regex LambdaRegex();

    [GeneratedRegex(@"<>c__DisplayClass\d+")]
    private static partial Regex ClosureRegex();

    [GeneratedRegex(@"`\d+")]
    private static partial Regex GenericArityRegex();
}
