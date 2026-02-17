using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Projects;

public record ProjectResponse(int Id, string Name, Platform Platform, string ApiKey);

public record ProjectDetailResponse(int Id, string Name, Platform Platform, string ApiKey, string Dsn, int? TeamId);

public record ProjectOverviewResponse(int Id, string Name, Platform Platform, int IssueCount, int EventCount);

public record CreateProjectRequest(string Name, Platform Platform, int? TeamId = null);

public record UpdateProjectRequest(string Name);
