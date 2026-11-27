using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Projects;

public record ProjectResponse(int Id, string Name, Platform Platform, string ApiKey);

public record ProjectDetailResponse(int Id, string Name, Platform Platform, string ApiKey, string Dsn, int? TeamId);

/// <param name="IssueCount">Issues in the project, lifetime.</param>
/// <param name="EventCount">Events in the project, lifetime.</param>
/// <param name="WindowDays">Length of the trailing window the in-window counts cover.</param>
/// <param name="IssuesInWindow">Issues first seen inside the window.</param>
/// <param name="EventsInWindow">Events captured inside the window.</param>
public record ProjectOverviewResponse(
    int Id,
    string Name,
    Platform Platform,
    int IssueCount,
    int EventCount,
    int WindowDays,
    int IssuesInWindow,
    int EventsInWindow);

public record CreateProjectRequest(string Name, Platform Platform, int? TeamId = null);

public record UpdateProjectRequest(string Name);
