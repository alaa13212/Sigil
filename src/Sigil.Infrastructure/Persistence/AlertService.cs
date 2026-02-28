using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models;
using Sigil.Application.Models.Alerts;
using Sigil.Domain.Entities;
using Sigil.Domain.Enums;

namespace Sigil.Infrastructure.Persistence;

internal class AlertService(
    SigilDbContext dbContext,
    IDateTime dateTime,
    IEnumerable<IAlertSender> senders,
    IAppConfigService appConfigService) : IAlertService
{
    
    #region CRUD
    
    public async Task<List<AlertRuleResponse>> GetRulesForProjectAsync(int projectId)
    {
        var rules = await dbContext.AlertRules
            .Include(r => r.AlertChannel)
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

        return rules.Select(ToResponse).ToList();
    }

    public async Task<AlertRuleResponse> CreateRuleAsync(int projectId, CreateAlertRuleRequest request)
    {
        var rule = new AlertRule
        {
            ProjectId = projectId,
            Name = request.Name,
            Trigger = request.Trigger,
            AlertChannelId = request.AlertChannelId,
            ThresholdCount = request.ThresholdCount,
            ThresholdWindow = request.ThresholdWindow,
            MinSeverity = request.MinSeverity,
            CooldownPeriod = request.CooldownPeriod ?? TimeSpan.FromMinutes(30),
            Enabled = request.Enabled,
            CreatedAt = dateTime.UtcNow
        };

        dbContext.AlertRules.Add(rule);
        await dbContext.SaveChangesAsync();

        await dbContext.Entry(rule).Reference(r => r.AlertChannel).LoadAsync();
        return ToResponse(rule);
    }

    public async Task<AlertRuleResponse?> UpdateRuleAsync(int ruleId, UpdateAlertRuleRequest request)
    {
        var rule = await dbContext.AlertRules.AsTracking()
            .Include(r => r.AlertChannel)
            .FirstOrDefaultAsync(r => r.Id == ruleId);
        if (rule is null) return null;

        rule.Name = request.Name;
        rule.Trigger = request.Trigger;
        rule.AlertChannelId = request.AlertChannelId;
        rule.ThresholdCount = request.ThresholdCount;
        rule.ThresholdWindow = request.ThresholdWindow;
        rule.MinSeverity = request.MinSeverity;
        rule.CooldownPeriod = request.CooldownPeriod;
        rule.Enabled = request.Enabled;

        await dbContext.SaveChangesAsync();

        if (rule.AlertChannel?.Id != request.AlertChannelId)
            await dbContext.Entry(rule).Reference(r => r.AlertChannel).LoadAsync();

        return ToResponse(rule);
    }

    public async Task<bool> DeleteRuleAsync(int ruleId)
    {
        var deleted = await dbContext.AlertRules.Where(r => r.Id == ruleId).ExecuteDeleteAsync();
        return deleted > 0;
    }

    public async Task<bool> ToggleRuleAsync(int ruleId, bool enabled)
    {
        var rule = await dbContext.AlertRules.AsTracking().FirstOrDefaultAsync(r => r.Id == ruleId);
        if (rule is null) return false;

        rule.Enabled = enabled;
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task SendTestAlertAsync(int ruleId)
    {
        var rule = await dbContext.AlertRules
            .Include(r => r.Project)
            .Include(r => r.AlertChannel)
            .FirstOrDefaultAsync(r => r.Id == ruleId);

        if (rule is null) throw new InvalidOperationException($"Alert rule {ruleId} not found.");

        var fakeIssue = new Issue
        {
            Id = 0,
            ProjectId = rule.ProjectId,
            Project = rule.Project,
            Title = "Test Alert Issue",
            ExceptionType = "TestException",
            Fingerprint = "test",
            Level = Severity.Error,
            Status = IssueStatus.Open,
            OccurrenceCount = 42,
            FirstSeen = dateTime.UtcNow.AddDays(-1),
            LastSeen = dateTime.UtcNow
        };

        var sender = GetSender(rule.AlertChannel!.Type);
        var baseUrl = GetBaseUrl();
        await sender.SendAsync(rule, fakeIssue, $"{baseUrl}/projects/{rule.ProjectId}/issues/0");
    }

    #endregion

    #region Evaluation

    public async Task EvaluateNewIssueAsync(Issue issue)
    {
        var rules = await GetEnabledRulesAsync(issue.ProjectId, AlertTrigger.NewIssue);
        foreach (var rule in rules)
        {
            if (rule.MinSeverity.HasValue && issue.Level < rule.MinSeverity.Value) continue;
            await FireAsync(rule, issue);
        }

        // Also fire NewHighSeverity rules
        if (issue.Level >= Severity.Error)
        {
            var highSevRules = await GetEnabledRulesAsync(issue.ProjectId, AlertTrigger.NewHighSeverity);
            foreach (var rule in highSevRules)
            {
                if (rule.MinSeverity.HasValue && issue.Level < rule.MinSeverity.Value) continue;
                await FireAsync(rule, issue);
            }
        }
    }

    public async Task EvaluateRegressionAsync(Issue issue)
    {
        var rules = await GetEnabledRulesAsync(issue.ProjectId, AlertTrigger.IssueRegression);
        foreach (var rule in rules)
        {
            if (rule.MinSeverity.HasValue && issue.Level < rule.MinSeverity.Value) continue;
            await FireAsync(rule, issue);
        }
    }

    public async Task EvaluateThresholdAsync(Issue issue)
    {
        var rules = await GetEnabledRulesAsync(issue.ProjectId, AlertTrigger.ThresholdExceeded);
        foreach (var rule in rules)
        {
            if (!rule.ThresholdCount.HasValue || !rule.ThresholdWindow.HasValue) continue;
            if (rule.MinSeverity.HasValue && issue.Level < rule.MinSeverity.Value) continue;

            var windowStart = dateTime.UtcNow - rule.ThresholdWindow.Value;
            var recentCount = await dbContext.Events
                .CountAsync(e => e.IssueId == issue.Id && e.Timestamp >= windowStart);

            if (recentCount >= rule.ThresholdCount.Value)
                await FireAsync(rule, issue);
        }
    }

    #endregion

    #region History

    public async Task<PagedResponse<AlertHistoryResponse>> GetAlertHistoryAsync(int projectId, int page = 1, int pageSize = 50)
    {
        var query = dbContext.AlertHistory
            .Where(h => h.AlertRule!.ProjectId == projectId)
            .Include(h => h.AlertRule)
            .Include(h => h.Issue)
            .OrderByDescending(h => h.FiredAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        var responses = items.Select(h => new AlertHistoryResponse(
            h.Id,
            h.AlertRuleId,
            h.AlertRule?.Name ?? "",
            h.IssueId,
            h.Issue?.Title,
            h.FiredAt,
            h.Status,
            h.ErrorMessage)).ToList();

        return new PagedResponse<AlertHistoryResponse>(responses, total, page, pageSize);
    }

    #endregion

    #region Helpers

    private string GetBaseUrl() =>
        appConfigService.HostUrl?.TrimEnd('/') ?? "";

    private async Task<List<AlertRule>> GetEnabledRulesAsync(int projectId, AlertTrigger trigger) =>
        await dbContext.AlertRules
            .Include(r => r.AlertChannel)
            .Where(r => r.ProjectId == projectId && r.Enabled && r.Trigger == trigger)
            .ToListAsync();

    private async Task FireAsync(AlertRule rule, Issue issue)
    {
        // Cooldown check
        var cutoff = dateTime.UtcNow - rule.CooldownPeriod;
        var recentFire = await dbContext.AlertHistory
            .AnyAsync(h => h.AlertRuleId == rule.Id && h.IssueId == issue.Id && h.FiredAt >= cutoff);

        if (recentFire)
        {
            await RecordAsync(rule, issue, AlertDeliveryStatus.Throttled, null);
            return;
        }

        var baseUrl = GetBaseUrl();
        var issueUrl = issue.Id > 0 ? $"{baseUrl}/projects/{issue.ProjectId}/issues/{issue.Id}" : "";
        var sender = GetSender(rule.AlertChannel!.Type);

        try
        {
            var success = await sender.SendAsync(rule, issue, issueUrl);
            await RecordAsync(rule, issue, success ? AlertDeliveryStatus.Sent : AlertDeliveryStatus.Failed,
                success ? null : "Sender returned failure.");
        }
        catch (Exception ex)
        {
            await RecordAsync(rule, issue, AlertDeliveryStatus.Failed, ex.Message);
        }
    }

    private async Task RecordAsync(AlertRule rule, Issue issue, AlertDeliveryStatus status, string? error)
    {
        dbContext.AlertHistory.Add(new AlertHistory
        {
            AlertRuleId = rule.Id,
            IssueId = issue.Id > 0 ? issue.Id : null,
            FiredAt = dateTime.UtcNow,
            Status = status,
            ErrorMessage = error
        });
        await dbContext.SaveChangesAsync();
    }

    private IAlertSender GetSender(AlertChannelType channel) =>
        senders.FirstOrDefault(s => s.Channel == channel)
        ?? throw new InvalidOperationException($"No sender registered for channel {channel}.");

    private static AlertRuleResponse ToResponse(AlertRule r) => new(
        r.Id, r.ProjectId, r.Name, r.Trigger,
        r.AlertChannelId, r.AlertChannel?.Name ?? "",
        r.ThresholdCount, r.ThresholdWindow, r.MinSeverity,
        r.CooldownPeriod, r.Enabled, r.CreatedAt);


    #endregion
}
