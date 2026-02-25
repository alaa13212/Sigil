using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.AutoTags;
using Sigil.Domain;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class AutoTagService(
    SigilDbContext dbContext,
    IAutoTagRuleCache cache,
    IDateTime dateTime) : IAutoTagService
{
    public async Task<List<AutoTagRuleResponse>> GetRulesForProjectAsync(int projectId)
    {
        var rules = await dbContext.AutoTagRules
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.Priority)
            .ToListAsync();
        return rules.Select(ToResponse).ToList();
    }

    public async Task<AutoTagRuleResponse> CreateRuleAsync(int projectId, CreateAutoTagRuleRequest request)
    {
        if (SystemTags.IsSystemTag(request.TagKey))
            throw new InvalidOperationException($"Cannot use the reserved '{SystemTags.Prefix}' prefix for auto-tag rules.");

        var rule = new AutoTagRule
        {
            ProjectId = projectId,
            Field = request.Field,
            Operator = request.Operator,
            Value = request.Value,
            TagKey = request.TagKey,
            TagValue = request.TagValue,
            Enabled = request.Enabled,
            Priority = request.Priority,
            Description = request.Description,
            CreatedAt = dateTime.UtcNow,
        };

        dbContext.AutoTagRules.Add(rule);
        await dbContext.SaveChangesAsync();
        cache.Invalidate(projectId);
        return ToResponse(rule);
    }

    public async Task<AutoTagRuleResponse?> UpdateRuleAsync(int ruleId, UpdateAutoTagRuleRequest request)
    {
        if (SystemTags.IsSystemTag(request.TagKey))
            throw new InvalidOperationException($"Cannot use the reserved '{SystemTags.Prefix}' prefix for auto-tag rules.");

        var rule = await dbContext.AutoTagRules.AsTracking().FirstOrDefaultAsync(r => r.Id == ruleId);
        if (rule is null) return null;

        rule.Field = request.Field;
        rule.Operator = request.Operator;
        rule.Value = request.Value;
        rule.TagKey = request.TagKey;
        rule.TagValue = request.TagValue;
        rule.Enabled = request.Enabled;
        rule.Priority = request.Priority;
        rule.Description = request.Description;

        await dbContext.SaveChangesAsync();
        cache.Invalidate(rule.ProjectId);
        return ToResponse(rule);
    }

    public async Task<bool> DeleteRuleAsync(int ruleId)
    {
        var rule = await dbContext.AutoTagRules.FirstOrDefaultAsync(r => r.Id == ruleId);
        if (rule is null) return false;

        await dbContext.AutoTagRules.Where(r => r.Id == ruleId).ExecuteDeleteAsync();
        cache.Invalidate(rule.ProjectId);
        return true;
    }

    public async Task<List<AutoTagRule>> GetRawRulesForProjectAsync(int projectId)
    {
        if (cache.TryGet(projectId, out var cached) && cached is not null)
            return cached;

        var rules = await dbContext.AutoTagRules
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.Priority)
            .ToListAsync();

        cache.Set(projectId, rules);
        return rules;
    }

    private static AutoTagRuleResponse ToResponse(AutoTagRule r) =>
        new(r.Id, r.ProjectId, r.Field, r.Operator, r.Value, r.TagKey, r.TagValue, r.Enabled, r.Priority, r.Description, r.CreatedAt);
}
