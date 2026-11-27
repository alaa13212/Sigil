using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Sigil.Server.Client.Pages.Issues;

namespace Sigil.Server.Tests;

/// <summary>
/// Guards how Razor binds component parameters. A bare attribute value is a string literal when the
/// parameter is a string, and a C# expression otherwise, so a member name handed to a string
/// parameter arrives as its own name instead of its value.
/// </summary>
public class RazorParameterBindingTests
{
    [Fact]
    public void A_string_parameter_that_receives_a_member_name_uses_an_at_sign()
    {
        var componentTypes = typeof(IssueList).Assembly.GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t))
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var offenses = new List<string>();

        foreach (var file in ClientRazorFiles())
        {
            var text = File.ReadAllText(file);
            var members = DeclaredMembers(text);

            foreach (var (tag, attributes) in ComponentTags(text))
            {
                if (!componentTypes.TryGetValue(tag, out var componentType)) continue;

                foreach (var (name, value) in attributes)
                {
                    if (!NamesAMember(value, members)) continue;

                    var property = componentType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (property?.PropertyType != typeof(string)) continue;

                    offenses.Add(
                        $"{Path.GetFileName(file)}: <{tag} {name}=\"{value}\"> hands a member name to a string parameter, " +
                        $"write {name}=\"@{value}\"");
                }
            }
        }

        offenses.Should().BeEmpty();
    }

    private static IEnumerable<string> ClientRazorFiles()
    {
        var project = Path.Combine(RepositoryRoot(), "src", "Sigil.Server.Client");

        return Directory.EnumerateFiles(project, "*.razor", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sigil.slnx")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the repository root holds Sigil.slnx");
        return directory!.FullName;
    }

    /// <summary>Names of the members a Razor file declares in its code block.</summary>
    private static HashSet<string> DeclaredMembers(string text)
    {
        var members = new HashSet<string>(StringComparer.Ordinal);
        var start = text.IndexOf("@code", StringComparison.Ordinal);
        if (start < 0) return members;

        foreach (Match match in MemberPattern().Matches(text[start..]))
            members.Add(match.Groups["name"].Value);

        return members;
    }

    private static IEnumerable<(string Tag, List<(string Name, string Value)> Attributes)> ComponentTags(string text)
    {
        foreach (Match match in ComponentTagPattern().Matches(text))
        {
            var attributes = AttributePattern()
                .Matches(match.Groups["attrs"].Value)
                .Select(m => (m.Groups["name"].Value, m.Groups["value"].Value))
                .ToList();

            yield return (match.Groups["tag"].Value, attributes);
        }
    }

    /// <summary>Whether the attribute value reads a member of the file that holds it.</summary>
    private static bool NamesAMember(string value, HashSet<string> members)
    {
        if (value.Length == 0 || value.Contains('@')) return false;
        if (!ReferencePathPattern().IsMatch(value)) return false;

        return members.Contains(value.Split('.')[0]);
    }

    private static Regex MemberPattern() => new(
        @"(?m)^\s*(?:\[[^\]]*\]\s*)*(?:public|private|protected|internal)\s+" +
        @"(?:(?:static|readonly|const|virtual|override|async|sealed|new|required)\s+)*" +
        @"[^\s(=;<>,\[\]]+(?:<[^<>]*>)?(?:\?|\[\])?\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?==|=>|\{|\(|;)",
        RegexOptions.Compiled);

    private static Regex ComponentTagPattern() => new(
        @"<(?<tag>[A-Z][A-Za-z0-9_]*)(?<attrs>[^>]*?)/?>",
        RegexOptions.Compiled);

    private static Regex AttributePattern() => new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled);

    private static Regex ReferencePathPattern() => new(
        @"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$",
        RegexOptions.Compiled);
}
