using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Tests.Persistence.Analyzers;

internal static class AnalyzerTestHelper
{
    public static readonly DateTime DefaultNow = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public static IDateTime StubDateTime(DateTime? utcNow = null)
    {
        var dt = Substitute.For<IDateTime>();
        dt.UtcNow.Returns(utcNow ?? DefaultNow);
        return dt;
    }

    /// <summary>Creates N events for the given issue/project, all within the last day of the default stubbed time by default.</summary>
    public static async Task CreateEventsAsync(SigilDbContext ctx, int projectId, int issueId, int count,
        Severity level = Severity.Error, string? userId = null, DateTime? baseTimestamp = null)
    {
        var ts = baseTimestamp ?? DefaultNow.AddDays(-1);
        for (int i = 0; i < count; i++)
        {
            ctx.Events.Add(new CapturedEvent
            {
                EventId = Guid.NewGuid().ToString("N")[..32],
                Timestamp = ts.AddMinutes(i),
                ReceivedAt = ts.AddMinutes(i),
                Level = level,
                Platform = Platform.CSharp,
                IssueId = issueId,
                ProjectId = projectId,
                UserId = userId,
                RawCompressedJson = [],
            });
        }
        await ctx.SaveChangesAsync();
    }
}
