using System.Text;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Domain.Extensions;
using Sigil.Infrastructure.Persistence;

namespace Sigil.Infrastructure.Workers;

internal class PostDigestionWorker(
    IServiceProvider services,
    ILogger<PostDigestionWorker> logger) : IWorker<PostDigestionWork>
{
    private readonly Channel<PostDigestionWork> _channel =
        Channel.CreateUnbounded<PostDigestionWork>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = false
        });

    public bool TryEnqueue(PostDigestionWork item) => _channel.Writer.TryWrite(item);

    public async Task RunAsync(CancellationToken stoppingToken = default)
    {
        await foreach (var work in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(work, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Post-digestion processing failed for project {ProjectId}", work.ProjectId);
            }
        }
    }

    private async Task ProcessAsync(PostDigestionWork work, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SigilDbContext>();

        var issues = await dbContext.Issues
            .Include(i => i.Project)
            .Where(i => work.IssueIds.Contains(i.Id))
            .ToListAsync(ct);

        await UpdateSearchColumnsAsync(work.IssueIds, dbContext, ct);
        await UpdateEventBucketsAsync(work.BucketIncrements, dbContext, ct);
        await RefreshMergeSetAggregates(issues, scope.ServiceProvider);
        await FireIssueAlerts(work, issues, scope.ServiceProvider);
        await LogPriorityChangesAsync(work.PriorityChanges, scope.ServiceProvider);
    }

    private static async Task UpdateSearchColumnsAsync(List<int> issueIds, SigilDbContext dbContext, CancellationToken ct)
    {
        var issues = await dbContext.Issues
            .AsTracking()
            .Where(i => issueIds.Contains(i.Id) && i.SuggestedEventId != null)
            .Include(i => i.SuggestedEvent!.StackFrames)
            .ToListAsync(ct);

        foreach (var issue in issues)
        {
            if (issue.SuggestedEvent is null) continue;
            issue.SuggestedEventMessage = issue.SuggestedEvent.Message?.Truncate(8192);
            issue.SuggestedFramesSummary = string.Join(" ",
                issue.SuggestedEvent.StackFrames
                    .Where(f => f.InApp)
                    .Select(f => $"{f.Module}.{f.Function} {f.Filename} {(f.Module + " " + f.Function + " " + f.Filename) .SplitPascal()}")).Truncate(8192);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private static async Task UpdateEventBucketsAsync(List<EventBucketIncrement> increments, SigilDbContext dbContext, CancellationToken ct)
    {
        if (increments.Count == 0) return;

        var sb = new StringBuilder();
        sb.Append("INSERT INTO \"EventBuckets\" (\"IssueId\", \"BucketStart\", \"Count\") VALUES ");

        for (int i = 0; i < increments.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"({increments[i].IssueId}, '{increments[i].BucketStart:yyyy-MM-dd HH:mm:ss}+00', {increments[i].Count})");
        }

        sb.Append(" ON CONFLICT (\"IssueId\", \"BucketStart\") DO UPDATE SET \"Count\" = \"EventBuckets\".\"Count\" + EXCLUDED.\"Count\"");

        await dbContext.Database.ExecuteSqlRawAsync(sb.ToString(), ct);
    }

    private static async Task RefreshMergeSetAggregates(List<Issue> issues, IServiceProvider serviceProvider)
    {
        var mergeSetService = serviceProvider.GetRequiredService<IMergeSetService>();
        var mergeSetIds = issues
            .Where(i => i.MergeSetId.HasValue)
            .Select(i => i.MergeSetId!.Value)
            .Distinct()
            .ToList();

        if (mergeSetIds.Count > 0)
            await mergeSetService.RefreshAggregatesAsync(mergeSetIds);
    }

    private static async Task LogPriorityChangesAsync(List<PriorityChange> changes, IServiceProvider serviceProvider)
    {
        IIssueActivityService activityService = serviceProvider.GetRequiredService<IIssueActivityService>();

        foreach (var change in changes)
        {
            await activityService.LogActivityAsync(
                change.IssueId,
                IssueActivityAction.PriorityChanged,
                userId: null,
                extra: new Dictionary<string, string>
                {
                    ["previous"] = change.OldPriority.ToString(),
                    ["new"] = change.NewPriority.ToString(),
                    ["reason"] = change.Reason,
                });
        }
    }

    private static async Task FireIssueAlerts(PostDigestionWork work, List<Issue> issues, IServiceProvider serviceProvider)
    {
        var alertService = serviceProvider.GetRequiredService<IAlertService>();
        foreach (Issue issue in issues)
        {
            if (work.NewIssueIds.Contains(issue.Id))
                await alertService.EvaluateNewIssueAsync(issue);
            else if (work.RegressionIssueIds.Contains(issue.Id))
                await alertService.EvaluateRegressionAsync(issue);

            await alertService.EvaluateThresholdAsync(issue);
        }
    }
}
