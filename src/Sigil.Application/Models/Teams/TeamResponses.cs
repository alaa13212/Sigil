using Sigil.Domain.Enums;

namespace Sigil.Application.Models.Teams;

public record TeamResponse(int Id, string Name, int MemberCount, int ProjectCount);

public record TeamDetailResponse(int Id, string Name, List<TeamMemberResponse> Members, List<TeamProjectResponse> Projects);

public record TeamMemberResponse(Guid UserId, string? DisplayName, string? Email, TeamRole Role);

public record TeamProjectResponse(int Id, string Name, Platform Platform);

public record CreateTeamRequest(string Name);

public record AddTeamMemberRequest(Guid UserId, TeamRole Role = TeamRole.Member);
