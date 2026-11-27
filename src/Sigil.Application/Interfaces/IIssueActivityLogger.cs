using Sigil.Application.Models.Issues;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Writes structured activity log entries (not comments) during server-side operations.</summary>
public interface IIssueActivityLogger
{
    Task<IssueActivity> LogActivityAsync(int issueId, IssueActivityAction action, Guid? userId = null, string? message = null, Dictionary<string, string>? extra = null);

    /// <summary>Writes one activity row per entry in a single unit of work.</summary>
    Task LogActivitiesAsync(IReadOnlyList<IssueActivityEntry> entries, Guid? userId = null);
}
