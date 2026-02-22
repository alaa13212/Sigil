using Microsoft.EntityFrameworkCore;
using Sigil.Application.Interfaces;
using Sigil.Application.Models.NormalizationRules;
using Sigil.Domain.Entities;

namespace Sigil.Infrastructure.Persistence;

internal class NormalizationRuleService(
    SigilDbContext dbContext,
    INormalizationRuleCache cache,
    IDateTime dateTime) : INormalizationRuleService
{
    private record TextNormalizationRulePreset(string Pattern, string Replacement, string Description);
    private static readonly List<TextNormalizationRulePreset> DefaultNormalizationRules =
    [
        // Specific typed values first (so they're not caught by generic patterns)
        new (@"\b(?:\d{1,3}\.){3}\d{1,3}(:\d{4,6})?\b", "{ip}", "IP Addresses"),
        new (@"\b[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\b", "{uuid}", "UUIDs"),
        new (@"\b\d{4}-\d{2}-\d{2}(?:[ T]\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?)?\b", "{datetime}", "Dates"),
        new (@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[A-Za-z]{2,}", "{email}", "Emails"),
        new (@"https?:\/\/[^\s/$.?#].[^\s]*", "{url}", "URLs"),
        new (@"/[A-Za-z0-9\-_]+(?:/[A-Za-z0-9\-_]+)*", "{urlpath}", "URL Paths"),
        new ("""(?:(?:[A-Za-z]:\\|\\\\)[^\\/:*?"<>|\r\n]+(?:\\[^\\/:*?"<>|\r\n]+)*|/(?:[^/\0]+/)*[^/\0]*)""", "{filepath}", "File Paths"),

        new (@"\b\d{10}\b", "{epoch}", "Epoch seconds"),
        new (@"\b\d{13}\b", "{epochms}", "Epoch millis"),
        
        new (@"\b([Tt][Rr][Uu][Ee]|[Ff][Aa][Ll][Ss][Ee])\b", "{bool}", "Boolean Values"),
        
        // Quoted strings
        new (@"""([^""\{\}:]*?)""", "\"{str}\"", "Quoted Strings (Souble Quotes)"),
        new (@"'([^'\{\}:]*?)'", "'{str}'", "Quoted Strings (Single Quotes)"),
        
        // Generic key-value pattern: "Key: value," or "Key: value)" or "Key: value]" or "Key: value}"
        // This normalizes variable values in structured messages
        new (@"(:\s*)\b(?=[^,(){}\[\]]*[^\d,(){}\[\]])[^,(){}\[\]]+\b(?=\s*[,)}\]])", "$1{val}", "Catch-all: Value of a Key: Value Pair"),
        
        new (@"\b\d+\b", "{int}", "Numbers"),
        new (@"\b[0-9A-Fa-f]{8,}\b", "{hex}", "Hexadecimal Numbers"),
    ];

    public List<TextNormalizationRule> CreateDefaultRulesPreset()
    {
        return DefaultNormalizationRules.Select((preset, index) => new TextNormalizationRule
        {
            Pattern = preset.Pattern,
            Replacement = preset.Replacement,
            Priority = 1000 + (DefaultNormalizationRules.Count - index) * 10,
            Enabled = true,
            Description = preset.Description,
            CreatedAt = DateTime.UtcNow
        }).ToList();
    }

    public async Task<List<TextNormalizationRule>> GetRulesAsync(int projectId)
    {
        var rules = await dbContext.TextNormalizationRules
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.Priority)
            .ToListAsync();
        return rules.ToList();
    }

    public async Task<TextNormalizationRule> CreateRuleAsync(int projectId, CreateNormalizationRuleRequest request)
    {
        var rule = new TextNormalizationRule
        {
            ProjectId = projectId,
            Pattern = request.Pattern,
            Replacement = request.Replacement,
            Priority = request.Priority,
            Enabled = request.Enabled,
            Description = request.Description,
            CreatedAt = dateTime.UtcNow,
        };

        dbContext.TextNormalizationRules.Add(rule);
        await dbContext.SaveChangesAsync();
        cache.Invalidate(projectId);
        return rule;
    }

    public async Task<TextNormalizationRule?> UpdateRuleAsync(int ruleId, UpdateNormalizationRuleRequest request)
    {
        var rule = await dbContext.TextNormalizationRules.AsTracking().FirstOrDefaultAsync(r => r.Id == ruleId);
        if (rule is null) return null;

        rule.Pattern = request.Pattern;
        rule.Replacement = request.Replacement;
        rule.Priority = request.Priority;
        rule.Enabled = request.Enabled;
        rule.Description = request.Description;

        await dbContext.SaveChangesAsync();
        cache.Invalidate(rule.ProjectId);
        return rule;
    }

    public async Task<bool> DeleteRuleAsync(int ruleId)
    {
        var rule = await dbContext.TextNormalizationRules.FirstOrDefaultAsync(r => r.Id == ruleId);
        if (rule is null) return false;

        await dbContext.TextNormalizationRules.Where(r => r.Id == ruleId).ExecuteDeleteAsync();
        cache.Invalidate(rule.ProjectId);
        return true;
    }

    public async Task<List<TextNormalizationRule>> GetRawRulesAsync(int projectId)
    {
        if (cache.TryGet(projectId, out var cached) && cached is not null)
            return cached;

        var rules = await dbContext.TextNormalizationRules
            .Where(r => r.ProjectId == projectId)
            .OrderByDescending(r => r.Priority)
            .ToListAsync();

        cache.Set(projectId, rules);
        return rules;
    }
    
}
