using Fido2NetLib;
using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Persistence.Analyzers;

internal class AnonymousUsersEverywhereAnalyzer(SigilDbContext dbContext, IDateTime dateTime) : IProjectAnalyzer
{
    public string AnalyzerId => "anonymous-users-everywhere";
    public bool IsRepeatable => false;

    public async Task<ProjectRecommendation?> AnalyzeAsync(Project project)
    {
        var cutoff = dateTime.UtcNow.AddDays(-7);

        var totalWithUser = await dbContext.Events
            .CountAsync(e => e.ProjectId == project.Id && e.Timestamp >= cutoff && e.UserId != null);
        if (totalWithUser < 20) 
            return null;

        var anonymousCount = await dbContext.Events
            .Where(e => e.ProjectId == project.Id && e.Timestamp >= cutoff && e.UserId != null)
            .CountAsync(e => e.User!.Identifier == null || e.User.Identifier == "anonymous");

        if ((double)anonymousCount / totalWithUser < 0.8) return null;

        return new ProjectRecommendation
        {
            ProjectId = project.Id,
            AnalyzerId = AnalyzerId,
            Severity = RecommendationSeverity.Info,
            Title = "Users are mostly anonymous",
            Description = "Most events with user context have no user ID or username set. Set a unique user ID after login so Sigil can track how many real users are affected by each issue.",
            ActionUrl = $"https://docs.sentry.io/platforms/{PlatformHelper.ToStringValue(project.Platform)}/enriching-events/identify-user/"
        };
    }
}
