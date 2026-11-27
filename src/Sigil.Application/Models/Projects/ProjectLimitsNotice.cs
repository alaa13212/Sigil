using System.Globalization;

namespace Sigil.Application.Models.Projects;

/// <summary>Describes a resolved limits record in the words an operator would use.</summary>
public static class ProjectLimitsNotice
{
    /// <summary>Describes the retention policy the worker enforces for a project.</summary>
    public static IReadOnlyList<string> Describe(ProjectLimitsResponse limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        var lines = new List<string>();

        lines.Add(limits.RetentionMaxAgeDaysOverride is null
            ? $"Events older than {limits.RetentionMaxAgeDays} days are deleted automatically. This project inherits the global default."
            : $"Events older than {limits.RetentionMaxAgeDays} days are deleted automatically. This project overrides the {limits.RetentionDefaultMaxAgeDays}-day global default.");

        lines.Add($"When this project holds more than {limits.RetentionMaxEvents.ToString("N0", CultureInfo.InvariantCulture)} events, the oldest are deleted first.");

        lines.Add($"The cleanup runs every {limits.RetentionCheckIntervalMinutes} minutes. Failed ingestions are kept for {limits.RetentionFailedEnvelopeMaxAgeDays} days.");

        return lines;
    }
}
