using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Services;

internal static class AlertTitleHelper
{
    private const int MaxLength = 80;

    public static string GetAlertTitle(Issue issue)
    {
        var title = issue.Title;

        // Fall back to ExceptionType + Culprit when title is absent
        if (string.IsNullOrWhiteSpace(title))
        {
            var parts = new[] { issue.ExceptionType, issue.Culprit }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            title = string.Join(": ", parts);
        }

        if (string.IsNullOrWhiteSpace(title))
            title = "No title";
        
        // Take only the first line
        if (!string.IsNullOrEmpty(title))
        {
            var newlineIndex = title.IndexOf('\n');
            if (newlineIndex >= 0)
                title = title[..newlineIndex].TrimEnd('\r');
        }
        
        // Truncate to MaxLength
        if (title.Length > MaxLength)
            title = title[..MaxLength].TrimEnd() + "…";

        return title;
    }
}
