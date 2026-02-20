using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sigil.Application.Interfaces;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Workers;

internal class RecommendationAnalysisWorker(
    IServiceProvider services,
    IOptions<BatchWorkersConfig> options,
    ILogger<RecommendationAnalysisWorker> logger) : IWorker
{
    private readonly TimeSpan _interval = options.Value.GetOptions(nameof(RecommendationAnalysisWorker)).FlushTimeout;

    public async Task RunAsync(CancellationToken stoppingToken = default)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AnalyzeAllProjectsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RecommendationAnalysisWorker encountered an error");
            }

            try { await Task.Delay(_interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task AnalyzeAllProjectsAsync(CancellationToken ct)
    {
        List<Project> projects;
        using (var scope = services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IProjectService>();
            projects = await service.GetAllProjectsAsync();
        }

        foreach (var project in projects)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                using var scope = services.CreateScope();
                var recommendationService = scope.ServiceProvider.GetRequiredService<IRecommendationService>();
                await recommendationService.RunAnalyzersAsync(project);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to run analyzers for project {ProjectId}", project.Id);
            }
        }
    }
}
