using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.IssueTrackers;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;
using Sigil.Infrastructure.Services;

namespace Sigil.Infrastructure.Persistence;

internal class IssueTrackerService(
    SigilDbContext dbContext,
    TokenEncryptionService tokenEncryption,
    IEnumerable<IIssueTrackerClient> clients,
    IDateTime dateTime,
    IAppConfigService appConfigService) : IIssueTrackerService
{
    public async Task<IssueTrackerConfigResponse> AddConfigAsync(int projectId, CreateTrackerConfigRequest request)
    {
        var encrypted = tokenEncryption.Encrypt(request.ConfigJson);

        var config = new IssueTrackerConfig
        {
            ProjectId = projectId,
            TrackerType = request.TrackerType,
            EncryptedConfig = encrypted,
            Enabled = true,
            TwoWaySync = false,
            CreatedAt = dateTime.UtcNow
        };

        dbContext.IssueTrackerConfigs.Add(config);
        await dbContext.SaveChangesAsync();
        return ToResponse(config);
    }

    public async Task<List<IssueTrackerConfigResponse>> GetConfigsAsync(int projectId)
    {
        var configs = await dbContext.IssueTrackerConfigs
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.TrackerType)
            .ToListAsync();

        return configs.Select(ToResponse).ToList();
    }

    public async Task<bool> UpdateConfigAsync(int configId, UpdateTrackerConfigRequest request)
    {
        var config = await dbContext.IssueTrackerConfigs.FirstOrDefaultAsync(c => c.Id == configId);
        if (config == null) return false;

        if (request.ConfigJson != null)
            config.EncryptedConfig = tokenEncryption.Encrypt(request.ConfigJson);

        if (request.Enabled.HasValue)
            config.Enabled = request.Enabled.Value;

        if (request.TwoWaySync.HasValue)
            config.TwoWaySync = request.TwoWaySync.Value;

        dbContext.IssueTrackerConfigs.Update(config);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteConfigAsync(int configId)
    {
        var deleted = await dbContext.IssueTrackerConfigs.Where(c => c.Id == configId).ExecuteDeleteAsync();
        return deleted > 0;
    }

    public async Task<bool> TestConfigAsync(int configId)
    {
        var config = await dbContext.IssueTrackerConfigs.FirstOrDefaultAsync(c => c.Id == configId);
        if (config == null) return false;

        var client = GetClient(config.TrackerType);
        var trackerConfig = new TrackerConfig(config.TrackerType, tokenEncryption.Decrypt(config.EncryptedConfig));

        try { return await client.TestConnectionAsync(trackerConfig); }
        catch { return false; }
    }

    public async Task<ExternalIssueLinkResponse> CreateExternalIssueAsync(int issueId, int trackerConfigId)
    {
        var trackerConfig = await dbContext.IssueTrackerConfigs.FirstOrDefaultAsync(c => c.Id == trackerConfigId)
            ?? throw new InvalidOperationException($"Tracker config {trackerConfigId} not found.");

        var issue = await dbContext.Issues
            .Select(i => new { i.Id, i.Title, i.ProjectId })
            .FirstOrDefaultAsync(i => i.Id == issueId)
            ?? throw new InvalidOperationException($"Issue {issueId} not found.");

        var issueUrl = $"{(appConfigService.HostUrl ?? "").TrimEnd('/')}/projects/{issue.ProjectId}/issues/{issueId}";

        var client = GetClient(trackerConfig.TrackerType);
        var config = new TrackerConfig(trackerConfig.TrackerType, tokenEncryption.Decrypt(trackerConfig.EncryptedConfig));

        var request = new CreateExternalIssueRequest(
            issue.Title ?? $"Issue #{issueId}",
            $"Issue reported in Sigil: {issue.Title}",
            issueUrl);

        var result = await client.CreateIssueAsync(config, request);

        var link = new ExternalIssueLink
        {
            IssueId = issueId,
            TrackerType = trackerConfig.TrackerType,
            ExternalId = result.ExternalId,
            ExternalUrl = result.ExternalUrl,
            ExternalStatus = result.InitialStatus,
            CreatedAt = dateTime.UtcNow
        };

        dbContext.ExternalIssueLinks.Add(link);
        await dbContext.SaveChangesAsync();
        return ToLinkResponse(link);
    }

    public async Task<List<ExternalIssueLinkResponse>> GetLinksAsync(int issueId)
    {
        var links = await dbContext.ExternalIssueLinks
            .Where(l => l.IssueId == issueId)
            .OrderBy(l => l.TrackerType)
            .ToListAsync();

        return links.Select(ToLinkResponse).ToList();
    }

    public async Task<bool> UnlinkAsync(int linkId)
    {
        var deleted = await dbContext.ExternalIssueLinks.Where(l => l.Id == linkId).ExecuteDeleteAsync();
        return deleted > 0;
    }

    private IIssueTrackerClient GetClient(TrackerType type) =>
        clients.FirstOrDefault(c => c.TrackerType == type)
        ?? throw new InvalidOperationException($"No client registered for {type}.");

    private static IssueTrackerConfigResponse ToResponse(IssueTrackerConfig c) =>
        new(c.Id, c.ProjectId, c.TrackerType, c.Enabled, c.TwoWaySync, c.CreatedAt);

    private static ExternalIssueLinkResponse ToLinkResponse(ExternalIssueLink l) =>
        new(l.Id, l.IssueId, l.TrackerType, l.ExternalId, l.ExternalUrl, l.ExternalStatus, l.CreatedAt, l.LastSyncedAt);
}
