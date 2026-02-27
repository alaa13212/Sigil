using System.Text.RegularExpressions;

namespace Sigil.Domain.Extensions;

public static partial class StringExtensions
{
    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex SplitPascalRegex();

    extension(string input)
    {
        public string SplitPascal() => SplitPascalRegex().Replace(input, "$1 $2");
    }
}