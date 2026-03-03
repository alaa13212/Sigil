using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Application.Interfaces;

/// <summary>Server-side only. Writes structured activity log entries (not comments) during server-side operations.</summary>
public interface IIssueActivityLogger
{
    Task<IssueActivity> LogActivityAsync(int issueId, IssueActivityAction action, Guid? userId = null, string? message = null, Dictionary<string, string>? extra = null);
}
