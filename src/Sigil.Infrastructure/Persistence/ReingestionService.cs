using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Reingestion;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Persistence;

internal class ReingestionService(
    SigilDbContext dbContext,
    IWorker<ReingestionWork> worker,
    IDateTime dateTime) : IReingestionService
{
    public async Task<ReingestionJobResponse> StartProjectReingestionAsync(int projectId, Guid? userId = null)
    {
        var hasActive = await dbContext.ReingestionJobs
            .AnyAsync(j => j.ProjectId == projectId &&
                           (j.Status == ReingestionJobStatus.Pending || j.Status == ReingestionJobStatus.Running));
        if (hasActive)
            throw new InvalidOperationException("A re-ingestion job is already active for this project.");

        var totalEvents = await dbContext.Events.CountAsync(e => e.ProjectId == projectId);

        var job = new ReingestionJob
        {
            ProjectId = projectId,
            Status = ReingestionJobStatus.Pending,
            TotalEvents = totalEvents,
            CreatedAt = dateTime.UtcNow,
            CreatedById = userId,
        };

        dbContext.ReingestionJobs.Add(job);
        await dbContext.SaveChangesAsync();

        worker.TryEnqueue(new ReingestionWork(job.Id));
        return await ToResponseAsync(job);
    }

    public async Task<ReingestionJobResponse> StartIssueReingestionAsync(int issueId, Guid? userId = null)
    {
        var issue = await dbContext.Issues.AsNoTracking().FirstOrDefaultAsync(i => i.Id == issueId);
        if (issue is null)
            throw new InvalidOperationException("Issue not found.");

        var hasActive = await dbContext.ReingestionJobs
            .AnyAsync(j => j.ProjectId == issue.ProjectId &&
                           (j.Status == ReingestionJobStatus.Pending || j.Status == ReingestionJobStatus.Running));
        if (hasActive)
            throw new InvalidOperationException("A re-ingestion job is already active for this project.");

        // Count events for this issue and any issues in the same merge set
        var issueIds = new List<int> { issueId };
        if (issue.MergeSetId.HasValue)
        {
            var mergeIssueIds = await dbContext.Issues
                .Where(i => i.MergeSetId == issue.MergeSetId.Value)
                .Select(i => i.Id)
                .ToListAsync();
            issueIds = mergeIssueIds;
        }

        var totalEvents = await dbContext.Events.CountAsync(e => e.IssueId == issueId);

        var job = new ReingestionJob
        {
            ProjectId = issue.ProjectId,
            IssueId = issueId,
            Status = ReingestionJobStatus.Pending,
            TotalEvents = totalEvents,
            CreatedAt = dateTime.UtcNow,
            CreatedById = userId,
        };

        dbContext.ReingestionJobs.Add(job);
        await dbContext.SaveChangesAsync();

        worker.TryEnqueue(new ReingestionWork(job.Id));
        return await ToResponseAsync(job);
    }

    public async Task<ReingestionJobResponse?> GetJobStatusAsync(int jobId)
    {
        var job = await dbContext.ReingestionJobs
            .AsNoTracking()
            .Include(j => j.CreatedBy)
            .FirstOrDefaultAsync(j => j.Id == jobId);
        return job is not null ? ToResponse(job) : null;
    }

    public async Task<List<ReingestionJobResponse>> GetJobsForProjectAsync(int projectId)
    {
        var jobs = await dbContext.ReingestionJobs
            .AsNoTracking()
            .Include(j => j.CreatedBy)
            .Where(j => j.ProjectId == projectId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(50)
            .ToListAsync();
        return jobs.Select(ToResponse).ToList();
    }

    public async Task<bool> CancelJobAsync(int jobId)
    {
        var job = await dbContext.ReingestionJobs.AsTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null) return false;

        if (job.Status is not (ReingestionJobStatus.Pending or ReingestionJobStatus.Running))
            return false;

        job.Status = ReingestionJobStatus.Cancelled;
        job.CompletedAt = dateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        return true;
    }

    private async Task<ReingestionJobResponse> ToResponseAsync(ReingestionJob job)
    {
        string? createdByName = null;
        if (job.CreatedById.HasValue)
        {
            createdByName = job.CreatedBy?.UserName;
            if (createdByName is null)
            {
                var user = await dbContext.Users.FindAsync(job.CreatedById.Value);
                createdByName = user?.UserName;
            }
        }

        return new ReingestionJobResponse(
            job.Id, job.ProjectId, job.IssueId, job.Status,
            job.TotalEvents, job.ProcessedEvents, job.MovedEvents, job.DeletedEvents,
            job.CreatedAt, job.StartedAt, job.CompletedAt, job.Error, createdByName);
    }

    private static ReingestionJobResponse ToResponse(ReingestionJob job) =>
        new(job.Id, job.ProjectId, job.IssueId, job.Status,
            job.TotalEvents, job.ProcessedEvents, job.MovedEvents, job.DeletedEvents,
            job.CreatedAt, job.StartedAt, job.CompletedAt, job.Error, job.CreatedBy?.UserName);
}
