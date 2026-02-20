using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Domain.Entities;
using Sigil.infrastructure.Persistence;

namespace Sigil.infrastructure.Workers;

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
            .Where(i => work.IssueIds.Contains(i.Id))
            .ToListAsync(ct);

        await RefreshMergeSetAggregates(issues, scope.ServiceProvider);
        await FireIssueAlerts(work, issues, scope.ServiceProvider);
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

    private static async Task FireIssueAlerts(PostDigestionWork work, List<Issue> issues, IServiceProvider serviceProvider)
    {
        var alertService = serviceProvider.GetRequiredService<IAlertService>();
        foreach (var issue in issues)
        {
            if (work.NewIssueIds.Contains(issue.Id))
                await alertService.EvaluateNewIssueAsync(issue);
            else if (work.RegressionIssueIds.Contains(issue.Id))
                await alertService.EvaluateRegressionAsync(issue);

            await alertService.EvaluateThresholdAsync(issue);
        }
    }
}
